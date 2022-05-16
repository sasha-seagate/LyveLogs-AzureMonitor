using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyveCloudToLogAnalytics
{
    internal class LogScanCheckpoint : TableEntity
    {
        public const string CheckpointId = "Checkpoint";
        public string Id { get; set; } = CheckpointId;
        public DateTime LastModificationTime { get; set; }
    }
}
