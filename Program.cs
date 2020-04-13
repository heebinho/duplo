using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace duplicates
{
    class Program
    {
        static readonly string[] Scopes = { DriveService.Scope.Drive, DriveService.Scope.DriveMetadata, DriveService.Scope.DriveFile };
        static readonly string ApplicationName = "Duplo";

        static void Main(string[] args)
        {
            UserCredential credential;

            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            FilesResource.ListRequest listRequest = service.Files.List();
            listRequest.PageSize = 1000;
            listRequest.Fields = "*";
            listRequest.Q = "mimeType = 'image/jpeg' or mimeType = 'image/png' or mimeType = 'video/mp4' or mimeType = 'video/mpeg'";

            FileList fileList = null;
            IList<Google.Apis.Drive.v3.Data.File> files = new List<Google.Apis.Drive.v3.Data.File>();
            do
            {
                if (fileList != null) listRequest.PageToken = fileList.NextPageToken;
                fileList = listRequest.Execute();
                files = files.Concat(fileList.Files).ToList();
            } while (fileList.NextPageToken != null);

            var fileGroups = files
                .GroupBy(g => new { g.Name, g.Size })
                .Where(c => c.Count() > 1);

            var duplicates = new List<Google.Apis.Drive.v3.Data.File>();
            string output = "";
            foreach (var group in fileGroups.OrderBy(o => o.Count()))
            {
                string groupInfo = string.Format("Group: {0} - Group Count: {1}", group.Key, group.Count());
                Console.WriteLine(groupInfo);
                output += groupInfo + Environment.NewLine;

                var origin = group.OrderBy(o => o.CreatedTimeRaw).FirstOrDefault();
                if (origin != null)
                {
                    string originInfo = string.Format("Origin:{0} - created:{1}", origin.Id, origin.CreatedTimeRaw);
                    Console.WriteLine(originInfo);
                    output += originInfo + Environment.NewLine;
                }

                foreach (var file in group)
                {
                    string indicatorSign = "+";
                    if (file.Id != origin.Id)
                    {
                        duplicates.Add(file);
                        indicatorSign = "-";
                    }
                    string info = string.Format("{0};{1};{2};{3};{4};{5};{6};",
                        indicatorSign, file.Id, file.Name, file.Size, file.CreatedTime.Value.ToShortDateString(),
                        file.Md5Checksum, file.Parents.Aggregate((a, b) => a + ";" + b));
                    Console.WriteLine(info);
                    output += info + Environment.NewLine;
                }
            }

            string delInfoMessage = "----------------------DELETE";
            Console.WriteLine(delInfoMessage);
            output += delInfoMessage + Environment.NewLine;

            long? total = 0;
            foreach (var duplicate in duplicates)
            {
                total += duplicate.Size;
                service.Files.Delete(duplicate.Id).Execute();
            }
            string delInfo = string.Format("Total bytes:{0}", total);
            Console.WriteLine(delInfo);
            output += delInfo + Environment.NewLine;


            System.IO.File.WriteAllText("grdrive.txt", output);
            Console.Read();


        }
    }
}
