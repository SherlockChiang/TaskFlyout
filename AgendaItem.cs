namespace Task_Flyout // ⚠️ 请确保命名空间与你的项目一致
{
    public class AgendaItem
    {
        public string Title { get; set; }     // 标题（例如：开发团队周会）
        public string Subtitle { get; set; }  // 副标题（例如时间，或者显示 "Google Tasks"）

        public bool IsTask { get; set; }      // 是不是任务（决定是否显示打勾框）
        public bool IsEvent { get; set; }     // 是不是日程（决定是否显示左侧颜色条）
    }
}