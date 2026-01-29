using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp2.UI.Controls
{
    public partial class VirtualKeyboardControl : UserControl
    {
        public static readonly DependencyProperty TargetTextBoxProperty =
            DependencyProperty.Register(nameof(TargetTextBox), typeof(TextBox), typeof(VirtualKeyboardControl),
                new PropertyMetadata(null, OnTargetTextBoxChanged));

        public TextBox TargetTextBox
        {
            get => (TextBox)GetValue(TargetTextBoxProperty);
            set => SetValue(TargetTextBoxProperty, value);
        }

        public VirtualKeyboardControl()
        {
            InitializeComponent();
            UpdateEnabledState();
        }

        private static void OnTargetTextBoxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VirtualKeyboardControl)d;
            control.UpdateEnabledState();
        }

        private void UpdateEnabledState()
        {
            IsEnabled = TargetTextBox != null;
        }

        private void InsertKey_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                return;
            }

            var key = button.Tag as string;
            if (key == null)
            {
                return;
            }

            InsertText(key);
        }

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
            if (TargetTextBox == null)
            {
                return;
            }

            var textBox = TargetTextBox;
            textBox.Focus();

            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;

            if (selectionLength > 0)
            {
                ReplaceSelection(string.Empty);
                return;
            }

            int caretIndex = textBox.CaretIndex;
            if (caretIndex <= 0 || caretIndex > textBox.Text.Length)
            {
                return;
            }

            textBox.Text = textBox.Text.Remove(caretIndex - 1, 1);
            textBox.CaretIndex = caretIndex - 1;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (TargetTextBox == null)
            {
                return;
            }

            TargetTextBox.Text = string.Empty;
            TargetTextBox.CaretIndex = 0;
            TargetTextBox.Focus();
        }

        private void MoveLeft_Click(object sender, RoutedEventArgs e)
        {
            MoveCaret(-1);
        }

        private void MoveRight_Click(object sender, RoutedEventArgs e)
        {
            MoveCaret(1);
        }

        private void MoveCaret(int delta)
        {
            if (TargetTextBox == null)
            {
                return;
            }

            var textBox = TargetTextBox;
            textBox.Focus();

            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;
            int caretIndex = textBox.CaretIndex;

            if (selectionLength > 0)
            {
                caretIndex = delta < 0 ? selectionStart : selectionStart + selectionLength;
                textBox.SelectionLength = 0;
            }

            caretIndex = Clamp(caretIndex + delta, 0, textBox.Text.Length);
            textBox.CaretIndex = caretIndex;
        }

        private void InsertText(string text)
        {
            if (TargetTextBox == null)
            {
                return;
            }

            if (text == null)
            {
                return;
            }

            var textBox = TargetTextBox;
            textBox.Focus();

            ReplaceSelection(text);
        }

        private void ReplaceSelection(string replacement)
        {
            if (TargetTextBox == null)
            {
                return;
            }

            var textBox = TargetTextBox;

            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;

            string currentText = textBox.Text ?? string.Empty;
            selectionStart = Clamp(selectionStart, 0, currentText.Length);
            selectionLength = Clamp(selectionLength, 0, currentText.Length - selectionStart);

            string before = currentText.Substring(0, selectionStart);
            string after = currentText.Substring(selectionStart + selectionLength);
            if (replacement == null)
            {
                replacement = string.Empty;
            }

            textBox.Text = before + replacement + after;
            textBox.CaretIndex = selectionStart + replacement.Length;
            textBox.SelectionLength = 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
