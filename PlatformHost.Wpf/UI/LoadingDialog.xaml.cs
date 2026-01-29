using System;
using System.Windows;
using System.Windows.Threading;

namespace WpfApp2.UI
{
    /// <summary>
    /// 简单的加载对话框
    /// </summary>
    public partial class LoadingDialog : Window
    {
        public LoadingDialog(string message = "正在处理，请稍候...")
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        /// <summary>
        /// 更新提示消息
        /// </summary>
        public void UpdateMessage(string message)
        {
            if (Dispatcher.CheckAccess())
            {
                MessageText.Text = message;
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => MessageText.Text = message), DispatcherPriority.Normal);
            }
        }

        /// <summary>
        /// 显示加载对话框（非阻塞方式）
        /// </summary>
        public new void Show()
        {
            try
            {
                if (Application.Current?.MainWindow != null && Application.Current.MainWindow != this)
                {
                    Owner = Application.Current.MainWindow;
                }
                base.Show();
                
                // 强制刷新显示
                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            }
            catch
            {
                // 如果设置Owner失败，直接显示
                try { base.Show(); } catch { }
            }
        }

        /// <summary>
        /// 关闭对话框（线程安全）
        /// </summary>
        public new void Close()
        {
            try
            {
                if (Dispatcher.CheckAccess())
                {
                    if (IsLoaded && !HasClosed())
                    {
                        base.Close();
                    }
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() => 
                    {
                        try
                        {
                            if (IsLoaded && !HasClosed())
                            {
                                base.Close();
                            }
                        }
                        catch { }
                    }), DispatcherPriority.Normal);
                }
            }
            catch { }
        }

        /// <summary>
        /// 检查窗口是否已关闭
        /// </summary>
        private bool HasClosed()
        {
            try
            {
                return !IsVisible;
            }
            catch
            {
                return true;
            }
        }
    }
} 