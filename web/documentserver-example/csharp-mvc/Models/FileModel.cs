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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using OnlineEditorsExampleMVC.Helpers;

namespace OnlineEditorsExampleMVC.Models
{
    public class FileModel
    {
        public string Mode { get; set; }
        public string Type { get; set; }

        public string FileUri
        {
            get { return DocManagerHelper.GetFileUri(FileName); }
        }

        public string FileName { get; set; }

        public string DocumentType
        {
            get { return FileUtility.GetFileType(FileName).ToString().ToLower(); }
        }

        public string Key
        {
            get { return ServiceConverter.GenerateRevisionId(DocManagerHelper.CurUserHostAddress() + "/" + FileName + "/" + File.GetLastWriteTime(DocManagerHelper.StoragePath(FileName, null)).GetHashCode()); }
        }

        public string CallbackUrl
        {
            get { return DocManagerHelper.GetCallback(FileName); }
        }

        public string GetDocConfig(HttpRequest request, UrlHelper url)
        {
            var jss = new JavaScriptSerializer();

            var ext = Path.GetExtension(FileName);
            var editorsMode = Mode ?? "edit";

            var canEdit = DocManagerHelper.EditedExts.Contains(ext);
            var mode = canEdit && editorsMode != "view" ? "edit" : "view";

            var actionLink = request.GetOrDefault("actionLink", null);
            var actionData = string.IsNullOrEmpty(actionLink) ? null : jss.DeserializeObject(actionLink);

            var config = new Dictionary<string, object>
                {
                    { "type", Type ?? "desktop" },
                    { "documentType", DocumentType },
                    {
                        "document", new Dictionary<string, object>
                            {
                                { "title", FileName },
                                { "url", FileUri },
                                { "fileType", ext.Trim('.') },
                                { "key", Key },
                                {
                                    "info", new Dictionary<string, object>
                                        {
                                            { "author", "Me" },
                                            { "created", DateTime.Now.ToShortDateString() }
                                        }
                                },
                                {
                                    "permissions", new Dictionary<string, object>
                                        {
                                            { "comment", editorsMode != "view" && editorsMode != "fillForms" && editorsMode != "embedded" && editorsMode != "blockcontent" },
                                            { "download", true },
                                            { "edit", canEdit && (editorsMode == "edit" || editorsMode == "filter") || editorsMode == "blockcontent" },
                                            { "fillForms", editorsMode != "view" && editorsMode != "comment" && editorsMode != "embedded" && editorsMode != "blockcontent" },
                                            { "modifyFilter", editorsMode != "filter" },
                                            { "modifyContentControl", editorsMode != "blockcontent" },
                                            { "review", editorsMode == "edit" || editorsMode == "review" }
                                        }
                                }
                            }
                    },
                    {
                        "editorConfig", new Dictionary<string, object>
                            {
                                { "actionLink", actionData },
                                { "mode", mode },
                                { "lang", request.Cookies.GetOrDefault("ulang", "en") },
                                { "callbackUrl", CallbackUrl },
                                {
                                    "user", new Dictionary<string, object>
                                        {
                                            { "id", request.Cookies.GetOrDefault("uid", "uid-1") },
                                            { "name", request.Cookies.GetOrDefault("uname", "John Smith") }
                                        }
                                },
                                {
                                    "embedded", new Dictionary<string, object>
                                        {
                                            { "saveUrl", FileUri },
                                            { "embedUrl", FileUri },
                                            { "shareUrl", FileUri },
                                            { "toolbarDocked", "top" }
                                        }
                                },
                                {
                                    "customization", new Dictionary<string, object>
                                        {
                                            { "about", true },
                                            { "feedback", true },
                                            {
                                                "goback", new Dictionary<string, object>
                                                    {
                                                        { "url", url.Action("Index", "Home") }
                                                    }
                                            }
                                        }
                                }
                            }
                    }
                };

            if (JwtManager.Enabled)
            {
                var token = JwtManager.Encode(config);
                config.Add("token", token);
            }

            return jss.Serialize(config);
        }

        public void GetHistory(out string history, out string historyData)
        {
            var jss = new JavaScriptSerializer();
            var histDir = DocManagerHelper.HistoryDir(DocManagerHelper.StoragePath(FileName, null));

            history = null;
            historyData = null;

            if (DocManagerHelper.GetFileVersion(histDir) > 0)
            {
                var currentVersion = DocManagerHelper.GetFileVersion(histDir);
                var hist = new List<Dictionary<string, object>>();
                var histData = new Dictionary<string, object>();

                for (var i = 0; i <= currentVersion; i++)
                {
                    var obj = new Dictionary<string, object>();
                    var dataObj = new Dictionary<string, object>();
                    var verDir = DocManagerHelper.VersionDir(histDir, i + 1);

                    var key = i == currentVersion ? Key : File.ReadAllText(Path.Combine(verDir, "key.txt"));

                    obj.Add("key", key);
                    obj.Add("version", i);

                    if (i == 0)
                    {
                        var infoPath = Path.Combine(histDir, "createdInfo.json");

                        if (File.Exists(infoPath))
                        {
                            var info = jss.Deserialize<Dictionary<string, object>>(File.ReadAllText(infoPath));
                            obj.Add("created", info["created"]);
                            obj.Add("user", new Dictionary<string, object>() {
                                { "id", info["id"] },
                                { "name", info["name"] },
                            });
                        }
                    }

                    dataObj.Add("key", key);
                    dataObj.Add("url", i == currentVersion ? FileUri : DocManagerHelper.GetPathUri(Directory.GetFiles(verDir, "prev.*")[0].Substring(HttpRuntime.AppDomainAppPath.Length)));
                    dataObj.Add("version", i);
                    if (i > 0)
                    {
                        var changes = jss.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(DocManagerHelper.VersionDir(histDir, i), "changes.json")));
                        var change = ((Dictionary<string, object>)((ArrayList)changes["changes"])[0]);

                        obj.Add("changes", changes["changes"]);
                        obj.Add("serverVersion", changes["serverVersion"]);
                        obj.Add("created", change["created"]);
                        obj.Add("user", change["user"]);

                        var prev = (Dictionary<string, object>)histData[(i - 1).ToString()];
                        dataObj.Add("previous", new Dictionary<string, object>() {
                            { "key", prev["key"] },
                            { "url", prev["url"] },
                        });
                        dataObj.Add("changesUrl", DocManagerHelper.GetPathUri(Path.Combine(DocManagerHelper.VersionDir(histDir, i), "diff.zip").Substring(HttpRuntime.AppDomainAppPath.Length)));
                    }

                    hist.Add(obj);
                    histData.Add(i.ToString(), dataObj);
                }

                history = jss.Serialize(new Dictionary<string, object>()
                {
                    { "currentVersion", currentVersion },
                    { "history", hist }
                });
                historyData = jss.Serialize(histData);
            }
        }
    }
}