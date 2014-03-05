//using System;
//using System.Collections.Generic;
//using System.Data.SQLite;
//using System.Linq;
//using System.Text;

//using Agent.Core.Utils;
//using System.IO;
//using System.Data;

//namespace Agent.RV.Data
//{
//    class SqliteRv
//    {
//        public const string ApplicationTable = "applications";

//        private string connectionString = String.Format(@"Data Source={0}", Path.Combine(Settings.DbDirectory, "rv.adb"));
//        public string ConnectionString { get { return connectionString; } }

//        public SqliteRv()
//        {
//            RecreateApplicationTable();
//        }

//        public void CreateApplicationTable()
//        {
//            try
//            {
//                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
//                {
//                    connection.Open();
//                    using (SQLiteCommand command = new SQLiteCommand(connection))
//                    {
//                        StringBuilder commandText = new StringBuilder();
//                        commandText.AppendFormat("CREATE TABLE IF NOT EXISTS [{0}] ", ApplicationTable);
//                        commandText.AppendFormat("([{0}] INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,", ApplicationColumn.Id);
//                        commandText.AppendFormat("[{0}] TEXT NOT NULL UNIQUE,", ApplicationColumn.VendorId);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.VendorName);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.Name);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.Description);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.Version);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.Urls);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.FileSize);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.SupportURL);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.VendorSeverity);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.TopPatchSeverity);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.KB);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.InstallDate);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.ReleaseDate);
//                        commandText.AppendFormat("[{0}] BOOLEAN NULL,", ApplicationColumn.Installed);
//                        commandText.AppendFormat("[{0}] TEXT NULL,", ApplicationColumn.CliOptions);
//                        commandText.AppendFormat("[{0}] BOOLEAN NULL)", ApplicationColumn.ThirdParty);

//                        command.CommandText = commandText.ToString();
//                        command.ExecuteNonQuery();
//                    }
//                    connection.Close();
//                }
//            }
//            catch (Exception e)
//            {                
//                Logger.Log("Could not create {0} table.", LogLevel.Error, ApplicationTable);
//                Logger.LogException(e);
//            }
            
//        }

//        public bool AddApplication(Application app)
//        {
//            if (DoesApplicationExist(app))
//            {
//                Logger.Log("Application {0} already exist. Ignoring.", LogLevel.Debug, app.VendorId);
//                return false;
//            }

//            bool result = false;
//            string insertString = String.Format(@"INSERT INTO {0} ( {1} ) VALUES ({2})", ApplicationTable, 
//                ApplicationColumn.AllColumns, "?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?");

//            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
//            {
//                connection.Open();
//                using (SQLiteTransaction transaction = connection.BeginTransaction())
//                {
//                    using (SQLiteCommand command = new SQLiteCommand(insertString, connection))
//                    {
//                        command.Transaction = transaction;

//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.VendorId), app.VendorId));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.VendorName), app.VendorName));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.Name), app.Name.Replace("\\", "\\\\").Replace("\"", "\\\"")));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.Description), app.Description.Replace("\\", "\\\\").Replace("\"", "\\\"")));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.Version), app.Version));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.Urls), UrlsToString(app.Uris)));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.FileSize), app.FileSize));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.SupportURL), app.SupportURL));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.VendorSeverity), app.VendorSeverity));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.TopPatchSeverity), app.TopPatchSeverity));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.KB), app.KB));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.InstallDate), app.InstallDate));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.ReleaseDate), app.ReleaseDate));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.Installed), app.Installed));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.CliOptions), app.CliOptions));
//                        command.Parameters.Add(new SQLiteParameter(String.Format("@{0}", ApplicationColumn.ThirdParty), app.ThirdParty));
//                        // When adding more command paramters, remember to update the insertString with '?'.

//                        try
//                        {
//                            command.ExecuteNonQuery();
//                            transaction.Commit();                            
//                            Logger.Log(@"Adding application: ""{0}"".", LogLevel.Debug, app.VendorId);
//                            result = true;
//                        }
//                        catch (SQLiteException e)
//                        {
//                            Logger.Log(@"Could not add application ""{0}"" to database.", LogLevel.Error, app.VendorId);
//                            Logger.LogException(e);
//                            transaction.Rollback();
//                        }
//                        finally
//                        {
//                            connection.Close();
//                        }
//                    }
//                }
//            }
//            return result;
//        }        

//        public void EditUpdate(Application update)
//        {
//            StringBuilder valueBuilder = new StringBuilder();
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.VendorName, update.VendorName);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.Name, update.Name);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.Description, update.Description);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.Version, update.Version);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.Urls, UrlsToString(update.Uris));
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.FileSize, update.FileSize);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.SupportURL, update.SupportURL);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.VendorSeverity, update.VendorSeverity);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.TopPatchSeverity, update.TopPatchSeverity);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.KB, update.KB);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.InstallDate, update.InstallDate);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.ReleaseDate, update.ReleaseDate);
//            valueBuilder.AppendFormat(@"{0} = {1}, ", ApplicationColumn.Installed, update.Installed);
//            valueBuilder.AppendFormat(@"{0} = ""{1}"", ", ApplicationColumn.CliOptions, update.CliOptions);
//            valueBuilder.AppendFormat(@"{0} = {1}", ApplicationColumn.ThirdParty, update.ThirdParty);

//            string values = valueBuilder.ToString();

//            string updateStatement = String.Format(@"UPDATE {0} SET {1} WHERE {2} = ""{3}""",
//                ApplicationTable, values, ApplicationColumn.VendorId, update.VendorId);

//            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
//            {
//                connection.Open();
//                using (SQLiteTransaction transaction = connection.BeginTransaction())
//                {
//                    using (SQLiteCommand command = new SQLiteCommand(updateStatement, connection))
//                    {
//                        command.Transaction = transaction;

//                        try
//                        {
//                            command.ExecuteNonQuery();
//                            transaction.Commit();
//                        }
//                        catch (SQLiteException e)
//                        {
//                            Logger.Log("Could not update vendor ID {0} .", LogLevel.Error, update.VendorId);
//                            Logger.LogException(e);
//                            transaction.Rollback();
//                        }
//                        finally
//                        {
//                            connection.Close();
//                        }
//                    }
//                }
//            }
//        }

//        /// <summary>
//        /// Returns an update by using either the vendor id first. Then tries by using the toppatch id.
//        /// </summary>
//        /// <param name="vendorId">An Update.</param>
//        /// <returns></returns>
//        public Application GetApplication(string vendorId)
//        {
//            Application update = null;
//            string selectSQL = String.Format(@"SELECT * FROM {0}  WHERE {1} = ""{2}"" LIMIT 1", ApplicationTable, ApplicationColumn.VendorId, vendorId);

//            try
//            {
//                DataTable table = GetDataTable(selectSQL);

//                if (table.Rows[0] != null)
//                {
//                    update = GetApplicationFromRow(table.Rows[0]);
//                }
//            }
//            catch (Exception e)
//            {
//                Logger.Log("Application ID # {0} not found.", LogLevel.Error, vendorId);
//                Logger.LogException(e);
//            }

//            return update;
//        }

//        public List<Application> GetAvailableUpdates()
//        {
//            return GetApplications(false);
//        }

//        public List<Application> GetInstalledApplications()
//        {
//            return GetApplications(true);
//        }

//        public List<Application> GetUpdatesAndApplications()
//        {
//            return GetApplications();
//        }

//        public List<Application> GetApplications(bool? installed = null)
//        {

//            List<Application> apps = new List<Application>();
//            string selectSQL = String.Format(@"SELECT * FROM {0}", ApplicationTable);

//            if(installed == true)
//                selectSQL = String.Format(@"SELECT * FROM {0} WHERE {1} = 1", ApplicationTable, ApplicationColumn.Installed);

//            else if (installed == false)
//                selectSQL = String.Format(@"SELECT * FROM {0} WHERE {1} = 0", ApplicationTable, ApplicationColumn.Installed);

//            try
//            {
//                DataTable table = GetDataTable(selectSQL);

//                foreach (DataRow row in table.Rows)
//                {
//                    Application app = GetApplicationFromRow(row);
//                    apps.Add(app);
//                }
//            }
//            catch (Exception e)
//            {
//                Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
//                if (e.InnerException != null)
//                {
//                    Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
//                }
//                Logger.Log("Failed to retrieve applications.", LogLevel.Error);
//            }
//            return apps;
//        }

//        private Application GetApplicationFromRow(DataRow row)
//        {
//            if (row == null)
//                return null;

//            Application app = new Application();

//            app.VendorId             = row[ApplicationColumn.VendorId].ToString();
//            app.VendorName           = row[ApplicationColumn.VendorName].ToString();
//            app.Name                 = row[ApplicationColumn.Name].ToString();
//            app.Description          = row[ApplicationColumn.Description].ToString();
//            app.Version              = row[ApplicationColumn.Version].ToString();
//            app.Uris                 = UrlsToIList(row[ApplicationColumn.Urls].ToString());
//            app.FileSize             = row[ApplicationColumn.FileSize].ToString();
//            app.SupportURL           = row[ApplicationColumn.SupportURL].ToString();
//            app.VendorSeverity       = row[ApplicationColumn.VendorSeverity].ToString();
//            app.TopPatchSeverity     = row[ApplicationColumn.TopPatchSeverity].ToString();
//            app.KB                   = row[ApplicationColumn.KB].ToString();
//            app.InstallDate          = row[ApplicationColumn.InstallDate].ToString();
//            app.ReleaseDate          = row[ApplicationColumn.ReleaseDate].ToString();
//            app.Installed            = Convert.ToBoolean(row[ApplicationColumn.Installed].ToString());
//            app.CliOptions           = row[ApplicationColumn.CliOptions].ToString();
//            app.ThirdParty           = Convert.ToBoolean(row[ApplicationColumn.ThirdParty].ToString());

//            return app;
//        }

//        public bool DoesTableExist(string tableName)
//        {
//            bool results = false;
//            try
//            {
//                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
//                {
//                    connection.Open();
//                    using (SQLiteCommand command = new SQLiteCommand(connection))
//                    {
//                        command.CommandText = String.Format(@"SELECT name FROM sqlite_master WHERE name='{0}'", tableName);

//                        var scalar = command.ExecuteScalar();
//                        scalar = (scalar == null) ? "" : scalar;

//                        if (tableName.Equals(scalar.ToString()))     // If the scalar return is equal to tableName, then table exist. 
//                        {
//                            results = true;
//                        }
//                        else
//                        {
//                            results = false;
//                        }
//                        command.Dispose();
//                    }
//                    connection.Close();
//                }
//            }
//            catch (Exception e)
//            {
//                Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
//                if (e.InnerException != null)
//                {
//                    Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
//                }
//                Logger.Log("Couldn't check if  {0} table exist.", LogLevel.Error, tableName);
//            }
//            return results;
//        }

//        public bool DoesApplicationExist(Application app)
//        {
//            bool results = false;
//            try
//            {
//                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
//                {
//                    connection.Open();
//                    using (SQLiteCommand command = new SQLiteCommand(connection))
//                    {
//                        command.CommandText = String.Format(@"SELECT * FROM {0} WHERE {1} ='{2}' LIMIT 1", ApplicationTable,
//                            ApplicationColumn.VendorId, app.VendorId);

//                        var scalar = command.ExecuteScalar();
//                        if ((Convert.ToInt32(scalar) == 0))      // If scalar == 0, then it doesn't exist.
//                        {
//                            results = false;
//                        }
//                        else
//                        {
//                            results = true;
//                        }
//                        command.Dispose();
//                    }
//                    connection.Close();
//                }
//            }
//            catch (Exception e)
//            {
//                Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
//                if (e.InnerException != null)
//                {
//                    Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
//                }
//                Logger.Log("Could not check if  {0} update exist.", LogLevel.Error, app.VendorId);
//            }
//            return results;
//        }

//        public DataTable GetDataTable(string sql)
//        {
//            DataTable table = new DataTable();
//            try
//            {
//                using (SQLiteConnection connection = new SQLiteConnection(connectionString))
//                {
//                    connection.Open();
//                    SQLiteCommand sqlcommand = new SQLiteCommand(sql, connection);
//                    using (SQLiteDataReader reader = sqlcommand.ExecuteReader())
//                    {
//                        table.Load(reader);
//                    }
//                }
//            }
//            catch (Exception e)
//            {
//                throw new Exception(e.Message);
//            }
//            return table;
//        }

//        public bool DropTable(string tableName)
//        {
//            bool result = false;
//            string dropStatement = String.Format(@"DROP TABLE IF EXISTS {0}", tableName);

//            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
//            {
//                connection.Open();
//                using (SQLiteTransaction transaction = connection.BeginTransaction())
//                {
//                    using (SQLiteCommand command = new SQLiteCommand(dropStatement, connection))
//                    {
//                        command.Transaction = transaction;

//                        try
//                        {
//                            command.ExecuteNonQuery();
//                            transaction.Commit();
//                            result = true;
//                        }
//                        catch (SQLiteException e)
//                        {
//                            Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
//                            if (e.InnerException != null)
//                            {
//                                Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
//                            }
//                            Logger.Log("Could not drop {0} table.", LogLevel.Error, tableName);
//                            transaction.Rollback();
//                        }
//                        finally
//                        {
//                            connection.Close();
//                        }
//                    }
//                }
//            }
//            return result;
//        }

//        public void RecreateApplicationTable()
//        {
//            DropTable(ApplicationTable);
//            CreateApplicationTable();
//        }

//        /// <summary>
//        /// Converts a IList of strings into a single string space by a separator.
//        /// Only really used to save the links of an Update as a single string in one DB row.
//        /// </summary>
//        /// <param name="urls">String urls.</param>
//        /// <returns></returns>
//        private const string UrlSeparator = ",";
//        public static string UrlsToString(IList<string> urls)
//        {
//            StringBuilder builder = new StringBuilder();
//            for(int i = 0; i < urls.Count; i++)
//            {
//                // Let's not add a comma to the last element.
//                if (((i+1) == urls.Count))
//                {
//                    builder.Append(urls[i]);
//                    break;
//                }
//                builder.Append(urls[i] + UrlSeparator);
//            }
//            return builder.ToString();
//        }

//        /// <summary>
//        /// Converts a string made up of urls into an IList.
//        /// Only really used to retrieve the links of an Update as a single IList.
//        /// </summary>
//        /// <param name="urls">IList urls.</param>
//        /// <returns></returns>
//        public static IList<string> UrlsToIList(string urls)
//        {
//            List<string> urlList = new List<string>(urls.Split(UrlSeparator.ToArray()));
//            return urlList;
//        }

//    }

//    public class ApplicationColumn
//    {
//        public static string Id                 = "id";
//        public static string VendorId           = "vendor_id";
//        public static string VendorName         = "vendor_name";
//        public static string Name               = "name";
//        public static string Description        = "description";
//        public static string Version            = "version";
//        public static string Urls               = "urls";
//        public static string FileSize           = "file_size";
//        public static string SupportURL         = "support_url";
//        public static string VendorSeverity     = "vendor_severity";
//        public static string TopPatchSeverity   = "toppatch_severity";
//        public static string KB                 = "kb";
//        public static string InstallDate        = "install_date";
//        public static string ReleaseDate        = "release_date";
//        public static string Installed          = "installed";
//        public static string CliOptions         = "cli_options";
//        public static string ThirdParty         = "supported_third_party";

//        public static string AllColumns
//        {
//            get
//            {
//                StringBuilder cols = new StringBuilder();
//                cols.AppendFormat(@"{0},", VendorId);
//                cols.AppendFormat(@"{0},", VendorName);
//                cols.AppendFormat(@"{0},", Name);
//                cols.AppendFormat(@"{0},", Description);
//                cols.AppendFormat(@"{0},", Version);
//                cols.AppendFormat(@"{0},", Urls);
//                cols.AppendFormat(@"{0},", FileSize);
//                cols.AppendFormat(@"{0},", SupportURL);
//                cols.AppendFormat(@"{0},", VendorSeverity);
//                cols.AppendFormat(@"{0},", TopPatchSeverity);
//                cols.AppendFormat(@"{0},", KB);
//                cols.AppendFormat(@"{0},", InstallDate);
//                cols.AppendFormat(@"{0},", ReleaseDate);
//                cols.AppendFormat(@"{0},", Installed);
//                cols.AppendFormat(@"{0},", CliOptions);
//                cols.AppendFormat(@"{0}", ThirdParty);

//                return cols.ToString();
//            }
//        }
//    }
//}
