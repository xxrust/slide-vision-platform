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
using VM.Core;
using VM.PlatformSDKCS;
using static WpfApp2.UI.Page1;

namespace WpfApp2.UI
{
    /// <summary>
    /// Page2.xaml 的交互逻辑
    /// </summary>
    public partial class Page2 : Page
    {
        private VmProcedure procedure = null;//流程
        private bool mSolutionIsLoad = false;  // 代表方案是否加载
        private bool isControlInitialized = false; // 标记控件是否已初始化

        public Page2()
        {
            InitializeComponent();
            PageManager2.Page2Instance = this; // 设置静态实例
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //VmSolution.Load("相机源触发.sol");
            //var procedure = VmSolution.Instance["流程1"] as VmProcedure;
            //mSolutionIsLoad = true;  // 代表方案已经加载
            //PageManager.Page1Instance.render1.ModuleSource = procedure;//用Page1.xaml.cs中的render绑定procedure
            //PageManager.Page1Instance.pkg.ModuleSource = procedure;

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
            string strMsg = null;
            try
            {
                if (mSolutionIsLoad == true)
                {
                    VmSolution.Instance.CloseSolution();
                    mSolutionIsLoad = false;  // 代表方案已经关闭
                }
                else
                {
                    strMsg = "No solution file.";
                    //listBoxResult.Items.Insert(0, strMsg);
                    return;
                }

            }
            catch (VmException ex)
            {
                strMsg = "CloseSolution failed. Error Code: " + Convert.ToString(ex.errorCode, 16);
                //listBoxResult.Items.Insert(0, strMsg);
                return;
            }
            strMsg = "CloseSolution success";
            //listBoxResult.Items.Insert(0, strMsg);
        }

        // 添加静态 PageManager 类
        public static class PageManager2
        {
            public static Page2 Page2Instance { get; set; }
        }

    }
}

 
