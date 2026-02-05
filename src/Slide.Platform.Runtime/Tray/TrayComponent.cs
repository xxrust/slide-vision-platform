using System;
using System.Collections.Generic;

namespace Slide.Platform.Runtime.Tray
{
    public sealed class TrayComponent
    {
        private readonly TrayDataManager _manager;
        private readonly ITrayRepository _repository;
        private TrayResultEventArgs _lastResult;

        public TrayComponent(TrayDataManager manager, ITrayRepository repository, TrayMappingMode mappingMode = TrayMappingMode.Snake)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            MappingMode = mappingMode;
        }

        public TrayMappingMode MappingMode { get; set; }

        public event EventHandler<TrayResultEventArgs> OnResultProcessed;
        public event EventHandler<TrayCompletedEventArgs> OnTrayCompleted;
        public event EventHandler<TrayErrorEventArgs> OnError;
        public event EventHandler<TrayRetestEventArgs> OnManualRetestRequested;

        public TrayData StartTray(int rows, int cols, string batchName)
        {
            try
            {
                var tray = _manager.CreateTray(rows, cols, batchName);
                _repository.SaveTrayHeader(tray);
                return tray;
            }
            catch (Exception ex)
            {
                RaiseError(null, null, null, DateTime.UtcNow, ex);
                throw;
            }
        }

        public MaterialData UpdateResult(string position, string result, string imagePath, DateTime detectionTime)
        {
            TrayData activeTray = null;
            TrayPosition? resolvedPosition = null;

            try
            {
                activeTray = _manager.CurrentTray ?? throw new InvalidOperationException("No active tray. Call StartTray first.");
                resolvedPosition = TrayPosition.Parse(position, activeTray.Rows, activeTray.Cols, MappingMode);

                var material = _manager.UpdateResult(resolvedPosition.Value.Row, resolvedPosition.Value.Col, result, imagePath, detectionTime);
                _repository.SaveMaterial(activeTray.TrayId, material);

                var resultArgs = new TrayResultEventArgs(resolvedPosition, result, imagePath, detectionTime);
                _lastResult = resultArgs;
                OnResultProcessed?.Invoke(this, resultArgs);

                if (_manager.CurrentTray == null)
                {
                    var completedTray = activeTray;
                    if (!completedTray.CompletedAt.HasValue)
                    {
                        completedTray.CompletedAt = DateTime.UtcNow;
                    }

                    _repository.UpdateTrayCompletion(completedTray.TrayId, completedTray.CompletedAt.Value);
                    OnTrayCompleted?.Invoke(this, new TrayCompletedEventArgs(completedTray, resultArgs));
                }

                return material;
            }
            catch (Exception ex)
            {
                RaiseError(resolvedPosition, result, imagePath, detectionTime, ex);
                throw;
            }
        }

        public TrayData CompleteTray()
        {
            try
            {
                var completed = _manager.CompleteCurrentTray();
                if (completed == null)
                {
                    return null;
                }

                if (!completed.CompletedAt.HasValue)
                {
                    completed.CompletedAt = DateTime.UtcNow;
                }

                _repository.UpdateTrayCompletion(completed.TrayId, completed.CompletedAt.Value);
                OnTrayCompleted?.Invoke(this, new TrayCompletedEventArgs(completed, _lastResult));
                return completed;
            }
            catch (Exception ex)
            {
                RaiseError(_lastResult?.Position, _lastResult?.Result, _lastResult?.ImagePath, DateTime.UtcNow, ex);
                throw;
            }
        }

        public void ResetCurrentTray()
        {
            try
            {
                _manager.ResetCurrentTray();
                _lastResult = null;
            }
            catch (Exception ex)
            {
                RaiseError(_lastResult?.Position, _lastResult?.Result, _lastResult?.ImagePath, DateTime.UtcNow, ex);
                throw;
            }
        }

        public TrayStatistics GetStatistics()
        {
            return _manager.GetStatistics();
        }

        public IReadOnlyList<TrayData> GetHistory(int limit)
        {
            return _repository.LoadRecentTrays(limit);
        }

        public void RequestManualRetest(string position)
        {
            TrayPosition? resolvedPosition = null;

            try
            {
                var activeTray = _manager.CurrentTray ?? throw new InvalidOperationException("No active tray. Call StartTray first.");
                resolvedPosition = TrayPosition.Parse(position, activeTray.Rows, activeTray.Cols, MappingMode);
                OnManualRetestRequested?.Invoke(this, new TrayRetestEventArgs(resolvedPosition.Value));
            }
            catch (Exception ex)
            {
                RaiseError(resolvedPosition, null, null, DateTime.UtcNow, ex);
                throw;
            }
        }

        private void RaiseError(TrayPosition? position, string result, string imagePath, DateTime detectionTime, Exception error)
        {
            OnError?.Invoke(this, new TrayErrorEventArgs(position, result, imagePath, detectionTime, error));
        }
    }
}
