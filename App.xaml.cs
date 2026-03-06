using System; // 必须引入，用于捕获 Exception
using System.Windows;
using System.Windows.Media.Imaging;
using H.NotifyIcon; // 引入托盘库
using Task_Flyout; // 引入你存放 FlyoutWindow 的命名空间

namespace Task_Flyout
{
    public partial class App : Application
    {
        private TaskbarIcon _taskbarIcon;
        private FlyoutWindow _flyoutWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 用 try-catch 把启动代码包起来，遇到任何致命错误都会弹窗报警，不再悄悄闪退！
            try
            {
                // 1. 实例化我们的悬浮窗
                _flyoutWindow = new FlyoutWindow();

                // 2. 初始化右下角托盘图标
                _taskbarIcon = new TaskbarIcon
                {
                    ToolTipText = "我的全局日历与待办"
                };

                // 【核心排错区】：单独处理图标加载
                try
                {
                    _taskbarIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/icon.ico"));
                }
                catch (Exception)
                {
                    // 如果因为没有 icon.ico 或者没设置为“资源”导致报错，就会弹这个框，但程序不会死！
                    MessageBox.Show("警告：找不到 icon.ico 图标，或者属性没有设置为 '资源(Resource)'！\n\n程序将继续运行，但右下角托盘可能会显示出一块透明/空白的区域，你依然可以尝试点击那个空白处。",
                                    "图标缺失", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                _taskbarIcon.ForceCreate();
                // 3. 监听托盘左键点击事件，呼出悬浮窗
                _taskbarIcon.TrayLeftMouseDown += (s, args) =>
                {
                    _flyoutWindow.ToggleFlyout();
                };

                // 4. (可选) 监听托盘双击，用来呼出你的 MainWindow 主管理界面
                _taskbarIcon.TrayMouseDoubleClick += (s, args) =>
                {
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                };
            }
            catch (Exception ex)
            {
                // 如果是其他导致闪退的错误，全部抓出来显示！
                MessageBox.Show($"程序启动失败！\n\n错误原因：{ex.Message}\n\n详细信息：{ex.StackTrace}",
                                "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _taskbarIcon?.Dispose(); // 退出清理托盘
            base.OnExit(e);
        }
    }
}