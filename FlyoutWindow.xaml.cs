using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Google.Apis.Calendar.v3;

namespace Task_Flyout
{
    // 定义一个专门用来做本地缓存的类
    public class AppCache
    {
        public HashSet<DateTime> MarkedDates { get; set; } = new HashSet<DateTime>();
        public Dictionary<string, List<AgendaItem>> DayItems { get; set; } = new Dictionary<string, List<AgendaItem>>();
    }

    public partial class FlyoutWindow : Window
    {
        private GoogleAuthService _googleAuth;
        public ObservableCollection<AgendaItem> AgendaItems { get; set; }

        // 缓存相关变量：存在用户的 AppData 目录中，绝对安全且持久
        private readonly string CacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskFlyout", "local_cache.json");
        private AppCache _localCache = new AppCache();

        public static readonly DependencyProperty MarkedDatesProperty =
            DependencyProperty.Register("MarkedDates", typeof(HashSet<DateTime>), typeof(FlyoutWindow), new PropertyMetadata(new HashSet<DateTime>()));

        public HashSet<DateTime> MarkedDates
        {
            get { return (HashSet<DateTime>)GetValue(MarkedDatesProperty); }
            set { SetValue(MarkedDatesProperty, value); }
        }

        public FlyoutWindow()
        {
            InitializeComponent();
            _googleAuth = new GoogleAuthService();
            AgendaItems = new ObservableCollection<AgendaItem>();
            AgendaListControl.ItemsSource = AgendaItems;

            LoadCache(); // 启动时第一件事就是加载秒开缓存

            MainCalendar.SelectedDate = DateTime.Today;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_googleAuth.CalendarSvc == null) await _googleAuth.AuthorizeAsync();

            await FetchMonthMarkers(DateTime.Today);
            await RefreshDataForDate(DateTime.Today);
        }

        // ======================= 缓存管理 =======================
        private void LoadCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    string json = File.ReadAllText(CacheFilePath);
                    _localCache = JsonSerializer.Deserialize<AppCache>(json) ?? new AppCache();
                }
            }
            catch { _localCache = new AppCache(); }
        }

        private void SaveCache()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath));
                File.WriteAllText(CacheFilePath, JsonSerializer.Serialize(_localCache));
            }
            catch { }
        }

        // ======================= 悬停滚动魔法 =======================
        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 劫持鼠标滚轮事件，无论 ScrollViewer 有没有焦点，只要鼠标在上面，强行让它滚动
            if (sender is ScrollViewer scv)
            {
                scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        // ======================= 刷新按钮 =======================
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (MainCalendar.SelectedDate.HasValue)
            {
                // 强制要求从云端拉取，跳过缓存显示
                await RefreshDataForDate(MainCalendar.SelectedDate.Value, forceCloud: true);
                await FetchMonthMarkers(DateTime.Today); // 顺便把红点也刷新一下
            }
        }

        // ======================= 数据抓取 =======================
        private async Task FetchMonthMarkers(DateTime monthDate)
        {
            // 秒开逻辑：先直接拿缓存里的点画上去
            if (_localCache.MarkedDates != null && _localCache.MarkedDates.Count > 0)
            {
                this.MarkedDates = new HashSet<DateTime>(_localCache.MarkedDates);
            }

            try
            {
                var firstDay = new DateTime(monthDate.Year, monthDate.Month, 1);
                var lastDay = firstDay.AddMonths(1);

                var eventsRequest = _googleAuth.CalendarSvc.Events.List("primary");
                eventsRequest.TimeMin = firstDay;
                eventsRequest.TimeMax = lastDay;
                eventsRequest.SingleEvents = true;

                var events = await eventsRequest.ExecuteAsync();

                var tempMarkers = new HashSet<DateTime>();
                if (events.Items != null)
                {
                    foreach (var ev in events.Items)
                    {
                        if (ev.Start.DateTime.HasValue) tempMarkers.Add(ev.Start.DateTime.Value.Date);
                        else if (!string.IsNullOrEmpty(ev.Start.Date) && DateTime.TryParse(ev.Start.Date, out var allDay))
                            tempMarkers.Add(allDay.Date);
                    }
                }

                this.MarkedDates = null;
                this.MarkedDates = tempMarkers;

                // 更新缓存并保存
                _localCache.MarkedDates = tempMarkers;
                SaveCache();
            }
            catch { }
        }

        private async void MainCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainCalendar.SelectedDate.HasValue)
            {
                DateTime selectedDate = MainCalendar.SelectedDate.Value;
                TxtSelectedDate.Text = selectedDate.Date == DateTime.Today ? "今天, 日程安排" : $"{selectedDate:yyyy年MM月dd日} 的日程";

                await RefreshDataForDate(selectedDate);
            }
        }

        private async Task RefreshDataForDate(DateTime date, bool forceCloud = false)
        {
            string dateKey = date.ToString("yyyy-MM-dd");

            // 秒开逻辑：如果不强制云端，且本地有缓存，瞬间呈现
            if (!forceCloud && _localCache.DayItems.ContainsKey(dateKey))
            {
                AgendaItems.Clear();
                foreach (var item in _localCache.DayItems[dateKey]) AgendaItems.Add(item);
            }
            else
            {
                AgendaItems.Clear();
                AgendaItems.Add(new AgendaItem { Title = "同步中...", Subtitle = "正在获取最新数据", IsEvent = true, IsTask = false });
            }

            try
            {
                if (_googleAuth.CalendarSvc == null) await _googleAuth.AuthorizeAsync();

                var newItems = new List<AgendaItem>();

                // 1. 获取 Google Calendar
                var eventsRequest = _googleAuth.CalendarSvc.Events.List("primary");
                eventsRequest.TimeMin = date.Date;
                eventsRequest.TimeMax = date.Date.AddDays(1);
                eventsRequest.SingleEvents = true;
                eventsRequest.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

                var events = await eventsRequest.ExecuteAsync();
                foreach (var ev in events.Items ?? Enumerable.Empty<Google.Apis.Calendar.v3.Data.Event>())
                {
                    newItems.Add(new AgendaItem { Title = ev.Summary, Subtitle = ev.Start.DateTime?.ToString("HH:mm") ?? "全天日程", IsEvent = true, IsTask = false });
                }

                // 2. 获取 Google Tasks
                var tasksRequest = _googleAuth.TasksSvc.Tasks.List("@default");
                tasksRequest.ShowHidden = true; // 开启抓取已完成任务
                var tasks = await tasksRequest.ExecuteAsync();

                foreach (var t in tasks.Items ?? Enumerable.Empty<Google.Apis.Tasks.v1.Data.Task>())
                {
                    bool isDone = t.Status == "completed";

                    if (!string.IsNullOrEmpty(t.Due) && DateTime.TryParse(t.Due, out var dueTime))
                    {
                        if (dueTime.Date != date.Date) continue;
                    }
                    else
                    {
                        if (date.Date != DateTime.Today) continue;
                        if (isDone && !string.IsNullOrEmpty(t.Completed) && DateTime.TryParse(t.Completed, out var compTime))
                        {
                            if (compTime.Date != DateTime.Today) continue;
                        }
                    }

                    newItems.Add(new AgendaItem { Id = t.Id, Title = t.Title, Subtitle = "Google Tasks", IsEvent = false, IsTask = true, IsCompleted = isDone });
                }

                if (newItems.Count == 0)
                    newItems.Add(new AgendaItem { Title = "这一天没有安排", Subtitle = "休息一下吧", IsEvent = true, IsTask = false });

                // 统一更新 UI 和 缓存
                AgendaItems.Clear();
                foreach (var item in newItems) AgendaItems.Add(item);

                _localCache.DayItems[dateKey] = newItems;
                SaveCache();
            }
            catch (Exception ex)
            {
                if (AgendaItems.Count > 0 && AgendaItems[0].Title == "同步中...")
                {
                    AgendaItems.Clear();
                    AgendaItems.Add(new AgendaItem { Title = "同步失败", Subtitle = ex.Message, IsEvent = true, IsTask = false });
                }
            }
        }

        // ======================= 双向同步控制 =======================
        private async void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is AgendaItem item)
            {
                if (!item.IsTask || string.IsNullOrEmpty(item.Id)) return;

                bool isDone = checkBox.IsChecked == true;

                try
                {
                    checkBox.IsEnabled = false;

                    var taskToUpdate = new Google.Apis.Tasks.v1.Data.Task { Id = item.Id, Status = isDone ? "completed" : "needsAction" };
                    var updateRequest = _googleAuth.TasksSvc.Tasks.Patch(taskToUpdate, "@default", item.Id);
                    await updateRequest.ExecuteAsync();

                    // 同步成功后，同时更新本地的缓存记录，防止下次打开复原
                    string dateKey = MainCalendar.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
                    if (_localCache.DayItems.ContainsKey(dateKey))
                    {
                        _localCache.DayItems[dateKey] = new List<AgendaItem>(AgendaItems);
                        SaveCache();
                    }
                }
                catch (Exception ex)
                {
                    item.IsCompleted = !isDone;
                    MessageBox.Show("任务同步失败，请检查网络。\n原因：" + ex.Message, "同步失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                finally { checkBox.IsEnabled = true; }
            }
        }
        
        // ======================= 窗口交互逻辑 =======================
        public void ToggleFlyout()
        {
            if (this.Visibility == Visibility.Visible) this.Hide();
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

        private void Window_Deactivated(object sender, EventArgs e) => this.Hide();
    }
}