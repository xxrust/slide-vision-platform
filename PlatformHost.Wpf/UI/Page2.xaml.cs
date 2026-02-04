using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static WpfApp2.UI.Page1;

namespace WpfApp2.UI
{
    /// <summary>
    /// Page2.xaml 的交互逻辑
    /// </summary>
    public partial class Page2 : Page
    {
        private bool isControlInitialized = false; // 标记控件是否已初始化

        public Page2()
        {
            InitializeComponent();
            PageManager2.Page2Instance = this; // 设置静态实例
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // 预留：此页面暂不执行算法平台操作
        }

        //退出界面
        //private void Button_Click_1(object sender, RoutedEventArgs e)
        //{
        //    var mainWindow = (MainWindow)Application.Current.MainWindow;
        //    mainWindow.ContentC.Content = mainWindow.frame1; // 设置内容为 Page1
        //    VmGlobalToolControl.Dispose();

        //}

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("当前页面不再直连算法平台。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 添加静态 PageManager 类
        public static class PageManager2
        {
            public static Page2 Page2Instance { get; set; }
        }

    }
}

 
