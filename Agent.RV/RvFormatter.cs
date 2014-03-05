using Agent.Core.ServerOperations;
using Newtonsoft.Json.Linq;

namespace Agent.RV
{
    public static class RvFormatter
    {
        public static string Applications(RvSofOperation operation)
        {
            // If there are no updates, then an empty list (JArray) will be returned.
            var json = new JObject();
            var jsonArray = new JArray();
            if (operation.Applications != null)
            {
                json.Add(OperationKey.OperationId, operation.Id);
                json.Add(OperationKey.Operation, operation.Type);

                foreach (var app in operation.Applications)
                {
                    var temp = new JObject();
                    var urlArray = new JArray();
                    var depArray = new JArray();

                    foreach (var urldata in app.FileData)
                    {
                        var urljson = new JObject();
                        urljson["file_hash"] = urldata.Hash;
                        urljson["file_uri"] = urldata.Uri;
                        urljson["file_name"] = urldata.FileName;
                        urljson["file_size"] = urldata.FileSize;

                        urlArray.Add(urljson);
                    }

                    temp["name"] = app.Name;
                    temp["vendor_name"] = app.VendorName;
                    temp["description"] = app.Description;
                    temp["version"] = app.Version;
                    temp["file_data"] = urlArray;
                    temp["support_url"] = app.SupportUrl;
                    temp["vendor_severity"] = app.VendorSeverity;
                    temp["kb"] = app.KB;
                    temp["repo"] = string.Empty;
                    temp["install_date"] = app.InstallDate;
                    temp["release_date"] = app.ReleaseDate;
                    temp["status"] = app.Status.ToLower();
                    temp["reboot_required"] = app.RebootRequired;
                    temp["dependencies"] = depArray;
                    jsonArray.Add(temp);
                }
            }

            json.Add(OperationKey.Data, jsonArray);
            return json.ToString();
        }

        public static string Install(RVsofResult operation)
        {
            var json = new JObject();
            json.Add(OperationKey.OperationId, operation.OperationId);
            json.Add("success", operation.Success.ToLower());
            json.Add("reboot_required", operation.RebootRequired.ToLower());
            json.Add("error", (string.IsNullOrEmpty(operation.Error) ? string.Empty : operation.Error));
            json.Add("app_id", operation.AppId);

            var appsToDeleteArray = new JArray();
            if (operation.AppsToDelete != null && (operation.AppsToDelete != null || operation.AppsToDelete.Count > 0))
            {
                foreach (var node in operation.AppsToDelete)
                {
                    var appsToDelete = new JObject();
                    appsToDelete["name"] = (string.IsNullOrEmpty(node.Name) ? string.Empty : node.Name);
                    appsToDelete["version"] = (string.IsNullOrEmpty(node.Version) ? string.Empty : node.Version);
                    appsToDeleteArray.Add(appsToDelete);
                }
            }

            var appsToAddArray = new JArray();
            var filedata2 = new JArray();
            var tempEmptyArray = new JArray();

            if (operation.AppsToAdd != null && (operation.AppsToAdd != null || operation.AppsToAdd.Count > 0))
            {
                foreach (var node in operation.AppsToAdd)
                {
                    var appsToAdd = new JObject();
                    appsToAdd["name"] = (string.IsNullOrEmpty(node.AppsToAdd.Name) ? string.Empty : node.AppsToAdd.Name);
                    appsToAdd["version"] = (string.IsNullOrEmpty(node.AppsToAdd.Version) ? string.Empty : node.AppsToAdd.Version);
                    appsToAdd["description"] = (string.IsNullOrEmpty(node.AppsToAdd.Description) ? string.Empty : node.AppsToAdd.Description);
                    appsToAdd["install_date"] = node.AppsToAdd.InstallDate;
                    appsToAdd["kb"] = (string.IsNullOrEmpty(node.AppsToAdd.KB) ? string.Empty : node.AppsToAdd.KB);
                    appsToAdd["reboot_required"] = (string.IsNullOrEmpty(node.AppsToAdd.RebootRequired) ? string.Empty : node.AppsToAdd.RebootRequired);
                    appsToAdd["release_date"] = node.AppsToAdd.ReleaseDate;
                    appsToAdd["file_data"] = filedata2;
                    appsToAdd["status"] = (string.IsNullOrEmpty(node.AppsToAdd.Status) ? string.Empty : node.AppsToAdd.Status);
                    appsToAdd["support_url"] = (string.IsNullOrEmpty(node.AppsToAdd.SupportUrl) ? string.Empty : node.AppsToAdd.SupportUrl);
                    appsToAdd["vendor_id"] = (string.IsNullOrEmpty(node.AppsToAdd.VendorId) ? string.Empty : node.AppsToAdd.VendorId);
                    appsToAdd["vendor_name"] = (string.IsNullOrEmpty(node.AppsToAdd.VendorName) ? string.Empty : node.AppsToAdd.VendorName);
                    appsToAdd["vendor_severity"] = (string.IsNullOrEmpty(node.AppsToAdd.VendorSeverity) ? string.Empty : node.AppsToAdd.VendorSeverity);
                    appsToAdd["repo"] = string.Empty;
                    appsToAdd["dependencies"] = tempEmptyArray;
                    appsToAddArray.Add(appsToAdd);
                }
            }

            var data = new JObject();
            data.Add("name", (string.IsNullOrEmpty(operation.Data.Name) ? string.Empty : operation.Data.Name));
            data.Add("description", (string.IsNullOrEmpty(operation.Data.Description) ? string.Empty : operation.Data.Description));
            data.Add("kb", (string.IsNullOrEmpty(operation.Data.Kb) ? string.Empty : operation.Data.Kb));
            data.Add("vendor_severity", (string.IsNullOrEmpty(operation.Data.VendorSeverity) ? string.Empty : operation.Data.VendorSeverity));
            data.Add("rv_severity", (string.IsNullOrEmpty(operation.Data.RvSeverity) ? string.Empty : operation.Data.RvSeverity));
            data.Add("support_url", (string.IsNullOrEmpty(operation.Data.SupportUrl) ? string.Empty : operation.Data.SupportUrl));
            data.Add("release_date", operation.Data.ReleaseDate);
            data.Add("vendor_id", (string.IsNullOrEmpty(operation.Data.VendorId) ? string.Empty : operation.Data.VendorId));
            data.Add("vendor_name", (string.IsNullOrEmpty(operation.Data.VendorName) ? string.Empty : operation.Data.VendorName));
            data.Add("repo", (string.IsNullOrEmpty(operation.Data.Repo) ? string.Empty : operation.Data.Repo));
            data.Add("version", (string.IsNullOrEmpty(operation.Data.Version) ? string.Empty : operation.Data.Version));

            var filedata = new JArray();
            foreach (var uri in operation.Data.FileData)
            {
                var jUri = new JObject();
                jUri["file_name"] = (string.IsNullOrEmpty(uri.FileName) ? string.Empty : uri.FileName);
                jUri["file_uri"] = (string.IsNullOrEmpty(uri.Uri) ? string.Empty : uri.Uri);
                jUri["file_size"] = uri.FileSize;
                jUri["file_hash"] = (string.IsNullOrEmpty(uri.Hash) ? string.Empty : uri.Hash);
                filedata.Add(jUri);
            }

            data.Add("file_data", filedata);

            json.Add("apps_to_delete", appsToDeleteArray);
            json.Add("apps_to_add", appsToAddArray);
            json.Add("data", data.ToString());

            return json.ToString();
        }

        public static string AgentUpdate(RVsofResult operation)
        {
            var json = new JObject();
            json.Add(OperationKey.OperationId, operation.OperationId);
            json.Add("success", operation.Success.ToLower());
            json.Add("reboot_required", operation.RebootRequired.ToLower());
            json.Add("error", (string.IsNullOrEmpty(operation.Error) ? string.Empty : operation.Error));
            json.Add("app_id", operation.AppId);

            var appsToDeleteArray = new JArray();
            if (operation.AppsToDelete != null && (operation.AppsToDelete != null || operation.AppsToDelete.Count > 0))
            {
                foreach (var node in operation.AppsToDelete)
                {
                    var appsToDelete = new JObject();
                    appsToDelete["name"] = (string.IsNullOrEmpty(node.Name) ? string.Empty : node.Name);
                    appsToDelete["version"] = (string.IsNullOrEmpty(node.Version) ? string.Empty : node.Version);
                    appsToDeleteArray.Add(appsToDelete);
                }
            }

            var appsToAddArray = new JArray();
            var filedata2 = new JArray();
            var tempEmptyArray = new JArray();

            if (operation.AppsToAdd != null && (operation.AppsToAdd != null || operation.AppsToAdd.Count > 0))
            {
                foreach (var node in operation.AppsToAdd)
                {
                    var appsToAdd = new JObject();
                    appsToAdd["name"] = (string.IsNullOrEmpty(node.AppsToAdd.Name) ? string.Empty : node.AppsToAdd.Name);
                    appsToAdd["version"] = (string.IsNullOrEmpty(node.AppsToAdd.Version) ? string.Empty : node.AppsToAdd.Version);
                    appsToAdd["description"] = (string.IsNullOrEmpty(node.AppsToAdd.Description) ? string.Empty : node.AppsToAdd.Description);
                    appsToAdd["install_date"] = node.AppsToAdd.InstallDate;
                    appsToAdd["kb"] = (string.IsNullOrEmpty(node.AppsToAdd.KB) ? string.Empty : node.AppsToAdd.KB);
                    appsToAdd["reboot_required"] = (string.IsNullOrEmpty(node.AppsToAdd.RebootRequired) ? string.Empty : node.AppsToAdd.RebootRequired);
                    appsToAdd["release_date"] = node.AppsToAdd.ReleaseDate;
                    appsToAdd["file_data"] = filedata2;
                    appsToAdd["status"] = (string.IsNullOrEmpty(node.AppsToAdd.Status) ? string.Empty : node.AppsToAdd.Status);
                    appsToAdd["support_url"] = (string.IsNullOrEmpty(node.AppsToAdd.SupportUrl) ? string.Empty : node.AppsToAdd.SupportUrl);
                    appsToAdd["vendor_id"] = (string.IsNullOrEmpty(node.AppsToAdd.VendorId) ? string.Empty : node.AppsToAdd.VendorId);
                    appsToAdd["vendor_name"] = (string.IsNullOrEmpty(node.AppsToAdd.VendorName) ? string.Empty : node.AppsToAdd.VendorName);
                    appsToAdd["vendor_severity"] = (string.IsNullOrEmpty(node.AppsToAdd.VendorSeverity) ? string.Empty : node.AppsToAdd.VendorSeverity);
                    appsToAdd["repo"] = string.Empty;
                    appsToAdd["dependencies"] = tempEmptyArray;
                    appsToAddArray.Add(appsToAdd);
                }
            }

            var data = new JObject();
            data.Add("name", operation.Data.Name);
            data.Add("description", operation.Data.Description);
            data.Add("kb", operation.Data.Kb);
            data.Add("vendor_severity", operation.Data.VendorSeverity);
            data.Add("rv_severity", (string.IsNullOrEmpty(operation.Data.RvSeverity) ? string.Empty : operation.Data.RvSeverity));
            data.Add("support_url", operation.Data.SupportUrl);
            data.Add("release_date", operation.Data.ReleaseDate);
            data.Add("vendor_id", operation.Data.VendorId);
            data.Add("vendor_name", operation.Data.VendorName);
            data.Add("repo", (string.IsNullOrEmpty(operation.Data.Repo) ? string.Empty : operation.Data.Repo));
            data.Add("version", operation.Data.Version);

            var filedata = new JArray();
            foreach (var uri in operation.Data.FileData)
            {
                var jUri = new JObject();
                jUri["file_name"] = (string.IsNullOrEmpty(uri.FileName) ? string.Empty : uri.FileName);
                jUri["file_uri"] = (string.IsNullOrEmpty(uri.Uri) ? string.Empty : uri.Uri);
                jUri["file_size"] = uri.FileSize;
                jUri["file_hash"] = (string.IsNullOrEmpty(uri.Hash) ? string.Empty : uri.Hash);
                filedata.Add(jUri);
            }

            data.Add("file_data", filedata);

            json.Add("apps_to_delete", appsToDeleteArray);
            json.Add("apps_to_add", appsToAddArray);
            json.Add("data", data.ToString());

            return json.ToString();
        }

    }
}
