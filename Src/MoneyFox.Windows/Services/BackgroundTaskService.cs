using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using MoneyFox.Shared.Interfaces;

namespace MoneyFox.Windows.Services {
    public class BackgroundTaskService : IBackgroundTaskService {
        private Dictionary<string, string> TimeTriggeredTasks => new Dictionary<string, string> {
            {"ClearPaymentBackgroundTask", "MoneyFox.TimeTriggeredTasks"},
            {"BackupBackgroundTask", "MoneyFox.TimeTriggeredTasks"}
        };

        public async Task RegisterTasksAsync() {
            foreach (var kvTask in TimeTriggeredTasks) {
                if (BackgroundTaskRegistration.AllTasks.Any(task => task.Value.Name == kvTask.Key)) {
                    continue;
                }

                await RegisterTimeTriggeredTaskAsync(kvTask.Key, kvTask.Value);
            }
        }

        private async Task RegisterTimeTriggeredTaskAsync(string taskName, string taskNamespace) {
            var requestAccess = await BackgroundExecutionManager.RequestAccessAsync();

            if (requestAccess == BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity ||
                requestAccess == BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity) {
                var taskBuilder = new BackgroundTaskBuilder {
                    Name = taskName,
                    TaskEntryPoint = string.Format("{0}.{1}", taskNamespace, taskName)
                };
                // Task will be executed all 6 hours
                // 360 = 6 * 60 Minutes
                taskBuilder.SetTrigger(new TimeTrigger(360, false));

                taskBuilder.Register();
            }
        }
    }
}