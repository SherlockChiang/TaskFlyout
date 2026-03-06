using System;
using System.Collections.ObjectModel;
using System.Windows;
using Google.Apis.Calendar.v3;

namespace Task_Flyout // ⚠️ 确保与你的项目一致
{
    public partial class FlyoutWindow : Window
    {
        private GoogleAuthService _googleAuth;

        // 这是一个专门用来绑定到界面的动态列表
        public ObservableCollection<AgendaItem> AgendaItems { get; set; }

        public FlyoutWindow()
        {
            InitializeComponent();
            _googleAuth = new GoogleAuthService();
            AgendaItems = new ObservableCollection<AgendaItem>();

            // 把数据列表告诉界面的 ItemsControl
            AgendaListControl.ItemsSource = AgendaItems;
        }

        // 当悬浮窗第一次加载时触发
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示加载提示
                AgendaItems.Add(new AgendaItem { Title = "正在连接 Google...", Subtitle = "请稍候", IsTask = true, IsEvent = false });

                // 1. 触发授权登录（第一次会自动弹浏览器，之后静默通过）
                await _googleAuth.AuthorizeAsync();

                // 2. 清空加载提示
                AgendaItems.Clear();

                // 3. 获取今天的日历日程
                var eventsRequest = _googleAuth.CalendarSvc.Events.List("primary");
                eventsRequest.TimeMin = DateTime.Today;
                eventsRequest.TimeMax = DateTime.Today.AddDays(1); // 获取今天一整天
                eventsRequest.SingleEvents = true;
                eventsRequest.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

                var events = await eventsRequest.ExecuteAsync();

                if (events.Items != null && events.Items.Count > 0)
                {
                    foreach (var eventItem in events.Items)
                    {
                        string timeString = eventItem.Start.DateTime?.ToString("HH:mm") ?? "全天";
                        AgendaItems.Add(new AgendaItem
                        {
                            Title = eventItem.Summary,
                            Subtitle = timeString,
                            IsEvent = true,
                            IsTask = false
                        });
                    }
                }

                // 4. 获取 Google Tasks 待办事项
                var tasksRequest = _googleAuth.TasksSvc.Tasks.List("@default"); // @default 代表默认任务列表
                tasksRequest.ShowHidden = false; // 不显示已完成的

                var tasks = await tasksRequest.ExecuteAsync();

                if (tasks.Items != null && tasks.Items.Count > 0)
                {
                    foreach (var taskItem in tasks.Items)
                    {
                        AgendaItems.Add(new AgendaItem
                        {
                            Title = taskItem.Title,
                            Subtitle = "Google Tasks",
                            IsEvent = false,
                            IsTask = true
                        });
                    }
                }

                if (AgendaItems.Count == 0)
                {
                    AgendaItems.Add(new AgendaItem { Title = "今天很清闲", Subtitle = "没有日程和任务", IsEvent = true });
                }
            }
            catch (Exception ex)
            {
                AgendaItems.Clear();
                AgendaItems.Add(new AgendaItem { Title = "获取失败", Subtitle = ex.Message, IsEvent = true });
            }
        }

        public void ToggleFlyout()
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                PositionWindow();
                this.Show();
                this.Activate();
            }
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Right - this.Width - 10;
            this.Top = workArea.Bottom - this.Height - 10;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Hide();
        }
    }
}