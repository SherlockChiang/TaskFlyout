using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Google.Apis.Tasks.v1;
using Google.Apis.Util.Store;

namespace Task_Flyout // ⚠️ 确保这里的命名空间和你的项目一致
{
    public class GoogleAuthService
    {
        // 我们需要的权限：读写日历，读写任务
        private static readonly string[] Scopes = { CalendarService.Scope.Calendar, TasksService.Scope.Tasks };
        private static readonly string ApplicationName = "Task Flyout";

        public CalendarService CalendarSvc { get; private set; }
        public TasksService TasksSvc { get; private set; }

        /// <summary>
        /// 启动授权流程并初始化服务
        /// </summary>
        public async Task AuthorizeAsync()
        {
            UserCredential credential;

            // 1. 读取我们刚才放进项目的 credentials.json
            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // 2. Token 会被自动保存在电脑的 MyDocuments/CalendarFlyout.Auth.Store 文件夹下
                // 这样除了第一次需要登录，以后程序都会自动静默刷新 Token
                string credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "CalendarFlyout.Auth.Store");

                // 3. 呼叫谷歌，这行代码会自动打开你的默认浏览器，让你点“授权”
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true));
            }

            // 4. 授权成功后，初始化日历服务
            CalendarSvc = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // 5. 初始化待办任务服务
            TasksSvc = new TasksService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
        }
    }
}