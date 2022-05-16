using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;


namespace LyveCloudToLogAnalytics
{
    /*
     * this funciong is running every hour
     * scans for new logs
     * saves new checkpoint
     * adds each new log to a queue
     * each addition to the queue triggers upload to Log Analytics process (DownloadAndIngestLogFile)
     */
    public class ScanLogs
    {
        AmazonS3Client S3Client;
        string BucketName = Utils.GetBucketName();
        string StartTime = Utils.GetStartTime();
        // this is my main working proj

        [FunctionName("ScanLogs")]
        public async Task RunAsync([TimerTrigger("*/10 * * * *")]TimerInfo myTimer, ILogger log, [Table("logrunscheckpoint", Connection = "AzureWebJobsStorage")] CloudTable logsCheckpoint, [Queue("logFiles"), StorageAccount("AzureWebJobsStorage")] ICollector<ScanLyveLogFileRequest> queue)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            // retrieves latest checkpoint
            var checkpoint = await GetLastCheckpointFileAsync(logsCheckpoint);

            // connect to lyve cloud S3
            S3Client = Utils.ConnectLyveS3();
            log.LogInformation($"Connected to Lyve S3: {DateTime.Now}");

            // get logs bucket
            S3Bucket logsBucket = await GetLogsBucket(log);

            // check if last checkpoint exists, if so, continue from it
            var lastModificationTime = DateTime.MinValue;
            if (checkpoint?.LastModificationTime != null)
            {
                lastModificationTime = checkpoint.LastModificationTime;
            }

            // check if the user wants ALL logs or from the last x hours only
            else if (!Utils.GetStartTime().Equals(""))
            {
                DateTime now = DateTime.Now.ToUniversalTime();
                lastModificationTime = now.AddHours(Int32.Parse(StartTime) * -1);
            }

            // init list that will hold logs data
            List<S3Object> allFiles = new List<S3Object>();
            ListObjectsV2Request request = new ListObjectsV2Request { BucketName = logsBucket.BucketName };
            ListObjectsV2Response response;

            // get list of logs from bucket
            do
            {
                response = await S3Client.ListObjectsV2Async(request);
                allFiles.AddRange(response.S3Objects.Where(x => x.Key.EndsWith("gz")).Where(x => x.LastModified.ToUniversalTime() > lastModificationTime).Where(x => x.Key.StartsWith("March")));

                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated);

            log.LogInformation($"Got {allFiles.Count()} new log files between {myTimer.ScheduleStatus.Last} to {DateTime.Now} ");


            // check if new logs were found. if so, update the last modified time
            if (allFiles.Count() > 0)
            {
                allFiles = allFiles.OrderByDescending(x => x.LastModified).ToList();
                lastModificationTime = allFiles[0].LastModified;
                //sets new checkpoint
                await SetCheckpointAsync(logsCheckpoint, lastModificationTime);
            }


            // add each new found log to the queue, which triggers download and injestion proceess
            foreach (var file in allFiles)
            {
                //Console.WriteLine(file.Key);
                AddToQueue(queue, logsBucket.BucketName, file.Key);
            }

        }


        /* Retrieves latest checkpoint (most reacent modified date) */
        private async Task<LogScanCheckpoint> GetLastCheckpointFileAsync(CloudTable logsCheckpoint)
        {
            var result = await logsCheckpoint.ExecuteAsync(TableOperation.Retrieve<LogScanCheckpoint>(LogScanCheckpoint.CheckpointId, LogScanCheckpoint.CheckpointId));
            var checkpoint = result.Result as LogScanCheckpoint;
            return checkpoint;
        }



        /* Retrieves and returns logs bucket as an object */
        private async Task<S3Bucket> GetLogsBucket(ILogger log)
        {
            ListBucketsResponse response = await S3Client.ListBucketsAsync();
            var logsBucket = response.Buckets.FirstOrDefault(b => b.BucketName == BucketName);

            // check if bucket exists
            if (logsBucket == null)
            {
                throw new Exception($"Bucket named '{BucketName}' doesn't exists. Can't get logs from Lyve S3");
            }

            //// check if bucket is empty
            //ListObjectsV2Request listRequest = new ListObjectsV2Request { BucketName = BucketName };
            //ListObjectsV2Response listResponse;
            //listResponse = await S3Client.ListObjectsV2Async(listRequest);
            //if (listResponse.S3Objects.Count == 0)
            //    log.LogInformation($"As of {DateTime.Now}, the bucket is empty.");


            return logsBucket;
        }


        /* Sets new checkpoint (most reacent modified date) */
        private async Task SetCheckpointAsync(CloudTable logsCheckpoint, DateTime lastModificationTime)
        {

            var checkpoint = new LogScanCheckpoint()
            {
                PartitionKey = LogScanCheckpoint.CheckpointId,
                RowKey = LogScanCheckpoint.CheckpointId,
                LastModificationTime = lastModificationTime,
                ETag = "*"
            };
            var operation = TableOperation.InsertOrReplace(checkpoint);
            await logsCheckpoint.ExecuteAsync(operation);
        }

        /* receives log data, adds it to queue (to download and injest it into Log Analytics) */
        private void AddToQueue([StorageAccount("AzureWebJobsStorage")] ICollector<ScanLyveLogFileRequest> queue, string bucketName, string fileUrl)
        {
            queue.Add(new ScanLyveLogFileRequest
            {
                BucketName = bucketName,
                FileUrl = fileUrl
            });
        }
    }
}
