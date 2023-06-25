using System;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.Internal;

using System.IO;
using Amazon.S3.Model;

namespace WbStarBot.Cloud
{
	public class AmazonHandler
	{
		private const string AccessKey = "ci72340";
		private const string SecretKey = "a62ca63535f2524b6833d69f62027aac";
		private const string S3Url = "https://s3.timeweb.com";
		private const string S3Bucket = "2b54508e-starsystemcloud";


        public void UploadData()
		{
			Task.Factory.StartNew(() =>
			{
				UploadFiles(OutputHandler.dataDir, "data");
            });
		}

		public bool UploadFiles(string directoryPath, string cloudDirectory)
		{
			IAmazonS3 client = new AmazonS3Client(AccessKey, SecretKey, new AmazonS3Config() { ServiceURL =  S3Url});

			TransferUtility utility = new TransferUtility(client);
            TransferUtilityUploadRequest request = new TransferUtilityUploadRequest();

			try
			{
				string[] files = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories).Concat(Directory.GetFiles(directoryPath, "*.txt", SearchOption.AllDirectories)).ToArray();

				foreach (string file in files)
				{
					request.BucketName = $@"{S3Bucket}/{cloudDirectory}";
					request.FilePath = file;
					request.Key = file.Substring(directoryPath.Length + 1);
					utility.Upload(request);
				}
				return true;
			}
			catch(Exception e)
			{
				Base.debugStream.Input(e);
				return false;
			}
        }
    }
}

