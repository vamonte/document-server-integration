﻿/**
 *
 * (c) Copyright Ascensio System SIA 2020
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Configuration;
using System.Web.Script.Serialization;
using System.Web.UI;
using ASC.Api.DocumentConverter;

namespace OnlineEditorsExample
{
    internal static class FileType
    {
        public static readonly List<string> ExtsSpreadsheet = new List<string>
            {
                ".xls", ".xlsx", ".xlsm",
                ".xlt", ".xltx", ".xltm",
                ".ods", ".fods", ".ots", ".csv"
            };

        public static readonly List<string> ExtsPresentation = new List<string>
            {
                ".pps", ".ppsx", ".ppsm",
                ".ppt", ".pptx", ".pptm",
                ".pot", ".potx", ".potm",
                ".odp", ".fodp", ".otp"
            };

        public static readonly List<string> ExtsDocument = new List<string>
            {
                ".doc", ".docx", ".docm",
                ".dot", ".dotx", ".dotm",
                ".odt", ".fodt", ".ott", ".rtf", ".txt",
                ".html", ".htm", ".mht",
                ".pdf", ".djvu", ".fb2", ".epub", ".xps"
            };

        public static string GetInternalExtension(string extension)
        {
            extension = Path.GetExtension(extension).ToLower();
            if (ExtsDocument.Contains(extension)) return ".docx";
            if (ExtsSpreadsheet.Contains(extension)) return ".xlsx";
            if (ExtsPresentation.Contains(extension)) return ".pptx";
            return string.Empty;
        }
    }

    public partial class _Default : Page
    {

        public static string VirtualPath
        {
            get
            {
                return
                    HttpRuntime.AppDomainAppVirtualPath
                    + (HttpRuntime.AppDomainAppVirtualPath.EndsWith("/") ? "" : "/")
                    + WebConfigurationManager.AppSettings["storage-path"]
                    + CurUserHostAddress(null) + "/";
            }
        }

        private static bool? _ismono;

        public static bool IsMono
        {
            get { return _ismono.HasValue ? _ismono.Value : (_ismono = (bool?)(Type.GetType("Mono.Runtime") != null)).Value; }
        }

        private static long MaxFileSize
        {
            get
            {
                long size;
                long.TryParse(WebConfigurationManager.AppSettings["filesize-max"], out size);
                return size > 0 ? size : 5*1024*1024;
            }
        }

        private static List<string> FileExts
        {
            get { return ViewedExts.Concat(EditedExts).Concat(ConvertExts).ToList(); }
        }

        private static List<string> ViewedExts
        {
            get { return (WebConfigurationManager.AppSettings["files.docservice.viewed-docs"] ?? "").Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
        }

        public static List<string> EditedExts
        {
            get { return (WebConfigurationManager.AppSettings["files.docservice.edited-docs"] ?? "").Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
        }

        public static List<string> ConvertExts
        {
            get { return (WebConfigurationManager.AppSettings["files.docservice.convert-docs"] ?? "").Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
        }

        private static string _fileName;

        public static string CurUserHostAddress(string userAddress)
        {
            return Regex.Replace(userAddress ?? HttpContext.Current.Request.UserHostAddress, "[^0-9a-zA-Z.=]", "_");
        }

        public static string StoragePath(string fileName, string userAddress)
        {
            var directory = HttpRuntime.AppDomainAppPath + WebConfigurationManager.AppSettings["storage-path"] + CurUserHostAddress(userAddress) + "\\";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return directory + Path.GetFileName(fileName);
        }

        public static string ForcesavePath(string fileName, string userAddress, Boolean create)
        {
            var directory = HttpRuntime.AppDomainAppPath + WebConfigurationManager.AppSettings["storage-path"] + CurUserHostAddress(userAddress) + "\\";
            if (!Directory.Exists(directory))
            {
                return "";
            }

            directory = directory + Path.GetFileName(fileName) + "-hist" + "\\";
            if (!Directory.Exists(directory))
            {
                if (create)
                {
                    Directory.CreateDirectory(directory);
                }
                else
                {
                    return "";
                }
            }

            directory = directory + Path.GetFileName(fileName);
            if (!File.Exists(directory))
            {
                if (!create)
                {
                    return "";
                }
            }

            return directory;
        }

        public static string HistoryDir(string storagePath)
        {
            return storagePath += "-hist";
        }

        public static string VersionDir(string histPath, int version)
        {
            return Path.Combine(histPath, version.ToString());
        }

        public static string VersionDir(string fileName, string userAddress, int version)
        {
            return VersionDir(HistoryDir(StoragePath(fileName, userAddress)), version);
        }

        public static int GetFileVersion(string historyPath)
        {
            if (!Directory.Exists(historyPath)) return 0;
            return Directory.EnumerateDirectories(historyPath).Count() + 1;
        }

        public static int GetFileVersion(string fileName, string userAddress)
        {
            return GetFileVersion(HistoryDir(StoragePath(fileName, userAddress)));
        }

        public static string FileUri(string fileName, Boolean forDocumentServer)
        {
            var uri = new UriBuilder(GetServerUrl(forDocumentServer));
            uri.Path = VirtualPath + fileName;
            return uri.ToString();
        }

        public static string GetServerUrl(Boolean forDocumentServer)
        {
            if (forDocumentServer && !WebConfigurationManager.AppSettings["files.docservice.url.example"].Equals(""))
            {
                return WebConfigurationManager.AppSettings["files.docservice.url.example"];
            }
            else
            {
                var uri = new UriBuilder(HttpContext.Current.Request.Url) { Query = "" };
                var requestHost = HttpContext.Current.Request.Headers["Host"];
                if (!string.IsNullOrEmpty(requestHost))
                    uri = new UriBuilder(uri.Scheme + "://" + requestHost);

                return uri.ToString();
            }
        }

        public static string DocumentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();

            if (FileType.ExtsDocument.Contains(ext)) return "word";
            if (FileType.ExtsSpreadsheet.Contains(ext)) return "cell";
            if (FileType.ExtsPresentation.Contains(ext)) return "slide";

            return "word";
        }

        protected string UrlPreloadScripts = WebConfigurationManager.AppSettings["files.docservice.url.site"] + WebConfigurationManager.AppSettings["files.docservice.url.preloader"];


        protected void Page_Load(object sender, EventArgs e)
        {
        }

        public static string DoUpload(HttpContext context)
        {
            var httpPostedFile = context.Request.Files[0];

            if (HttpContext.Current.Request.Browser.Browser.ToUpper() == "IE")
            {
                var files = httpPostedFile.FileName.Split(new char[] { '\\' });
                _fileName = files[files.Length - 1];
            }
            else
            {
                _fileName = httpPostedFile.FileName;
            }

            var curSize = httpPostedFile.ContentLength;
            if (MaxFileSize < curSize || curSize <= 0)
            {
                throw new Exception("File size is incorrect");
            }

            var curExt = (Path.GetExtension(_fileName) ?? "").ToLower();
            if (!FileExts.Contains(curExt))
            {
                throw new Exception("File type is not supported");
            }

            _fileName = GetCorrectName(_fileName);

            var savedFileName = StoragePath(_fileName, null);
            httpPostedFile.SaveAs(savedFileName);

            var histDir = HistoryDir(savedFileName);
            Directory.CreateDirectory(histDir);
            File.WriteAllText(Path.Combine(histDir, "createdInfo.json"), new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
                { "created", DateTime.Now.ToString("yyyy'-'MM'-'dd HH':'mm':'ss") },
                { "id", context.Request.Cookies.GetOrDefault("uid", "uid-1") },
                { "name", context.Request.Cookies.GetOrDefault("uname", "John Smith") }
            }));

            return _fileName;
        }

        public static string DoUpload(string fileUri, HttpRequest request)
        {
            _fileName = GetCorrectName(Path.GetFileName(fileUri));

            var curExt = (Path.GetExtension(_fileName) ?? "").ToLower();
            if (!FileExts.Contains(curExt))
            {
                throw new Exception("File type is not supported");
            }

            var req = (HttpWebRequest)WebRequest.Create(fileUri);

            try
            {
                // hack. http://ubuntuforums.org/showthread.php?t=1841740
                if (IsMono)
                {
                    ServicePointManager.ServerCertificateValidationCallback += (s, ce, ca, p) => true;
                }

                using (var stream = req.GetResponse().GetResponseStream())
                {
                    if (stream == null) throw new Exception("stream is null");
                    const int bufferSize = 4096;

                    using (var fs = File.Open(StoragePath(_fileName, null), FileMode.Create))
                    {
                        var buffer = new byte[bufferSize];
                        int readed;
                        while ((readed = stream.Read(buffer, 0, bufferSize)) != 0)
                        {
                            fs.Write(buffer, 0, readed);
                        }
                    }
                }

                var histDir = HistoryDir(StoragePath(_fileName, null));
                Directory.CreateDirectory(histDir);
                File.WriteAllText(Path.Combine(histDir, "createdInfo.json"), new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
                    { "created", DateTime.Now.ToString("yyyy'-'MM'-'dd HH':'mm':'ss") },
                    { "id", request.Cookies.GetOrDefault("uid", "uid-1") },
                    { "name", request.Cookies.GetOrDefault("uname", "John Smith") }
                }));
            }
            catch (Exception)
            {

            }
            return _fileName;
        }

        public static string DoConvert(HttpContext context)
        {
            _fileName = Path.GetFileName(context.Request["filename"]);

            var extension = (Path.GetExtension(_fileName) ?? "").Trim('.');
            var internalExtension = FileType.GetInternalExtension(_fileName).Trim('.');

            if (ConvertExts.Contains("." + extension)
                && !string.IsNullOrEmpty(internalExtension))
            {
                var key = ServiceConverter.GenerateRevisionId(FileUri(_fileName, true));

                string newFileUri;
                var result = ServiceConverter.GetConvertedUri(FileUri(_fileName, true), extension, internalExtension, key, true, out newFileUri);
                if (result != 100)
                {
                    return "{ \"step\" : \"" + result + "\", \"filename\" : \"" + _fileName + "\"}";
                }

                var fileName = GetCorrectName(Path.GetFileNameWithoutExtension(_fileName) + "." + internalExtension);

                var req = (HttpWebRequest)WebRequest.Create(newFileUri);

                // hack. http://ubuntuforums.org/showthread.php?t=1841740
                if (IsMono)
                {
                    ServicePointManager.ServerCertificateValidationCallback += (s, ce, ca, p) => true;
                }

                using (var stream = req.GetResponse().GetResponseStream())
                {
                    if (stream == null) throw new Exception("Stream is null");
                    const int bufferSize = 4096;

                    using (var fs = File.Open(StoragePath(fileName, null), FileMode.Create))
                    {
                        var buffer = new byte[bufferSize];
                        int readed;
                        while ((readed = stream.Read(buffer, 0, bufferSize)) != 0)
                        {
                            fs.Write(buffer, 0, readed);
                        }
                    }
                }

                var storagePath = StoragePath(_fileName, null);
                var histDir = HistoryDir(storagePath);
                File.Delete(storagePath);
                if (Directory.Exists(histDir)) Directory.Delete(histDir, true);

                _fileName = fileName;
                histDir = HistoryDir(StoragePath(_fileName, null));
                Directory.CreateDirectory(histDir);
                File.WriteAllText(Path.Combine(histDir, "createdInfo.json"), new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
                    { "created", DateTime.Now.ToString() },
                    { "id", context.Request.Cookies.GetOrDefault("uid", "uid-1") },
                    { "name", context.Request.Cookies.GetOrDefault("uname", "John Smith") }
                }));
            }

            return "{ \"filename\" : \"" + _fileName + "\"}";
        }

        public static string GetCorrectName(string fileName, string userAddress = null)
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var name = baseName + ext;

            for (var i = 1; File.Exists(StoragePath(name, userAddress)); i++)
            {
                name = baseName + " (" + i + ")" + ext;
            }
            return name;
        }

        protected static List<FileInfo> GetStoredFiles()
        {
            var directory = HttpRuntime.AppDomainAppPath + WebConfigurationManager.AppSettings["storage-path"] + CurUserHostAddress(null) + "\\";
            if (!Directory.Exists(directory)) return new List<FileInfo>();

            var directoryInfo = new DirectoryInfo(directory);

            List<FileInfo> storedFiles = directoryInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly).ToList();
            return storedFiles;
        }

        public static List<Dictionary<string, object>> GetFilesInfo(string fileId = null)
        {
            var files = new List<Dictionary<string, object>>();

            foreach (var file in GetStoredFiles())
            {
                var dictionary = new Dictionary<string, object>();
                dictionary.Add("version", GetFileVersion(file.Name, null));
                dictionary.Add("id", ServiceConverter.GenerateRevisionId(_Default.CurUserHostAddress(null) + "/" + file.Name + "/" + File.GetLastWriteTime(_Default.StoragePath(file.Name, null)).GetHashCode()));
                dictionary.Add("contentLength", Math.Round(file.Length / 1024.0, 2) + " KB");
                dictionary.Add("pureContentLength", file.Length);
                dictionary.Add("title", file.Name);
                dictionary.Add("updated", file.LastWriteTime.ToString());
                if (fileId != null)
                {
                    if (fileId.Equals(dictionary["id"]))
                    {
                        files.Add(dictionary);
                        break;
                    }
                }
                else
                {
                    files.Add(dictionary);
                }
            }

            return files;
        }
    }
}