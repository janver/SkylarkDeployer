﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using LibGit2Sharp;

namespace Mygod.Skylark.Deployer
{
    public partial class Step3 : Page
    {
        protected void Page_Load(object sender, EventArgs e) { }

        protected void Process()
        {
            string name = Request.Form["name"], dir = Server.MapPath("~/Content/"),
                   result = Api("https://appharbor.com/tokens", "client_id=044928e2-f4f7-4209-" +
                                "bdb6-388129e19fb0&client_secret=ae8f5e3d-1378-4ca9-9b3c-b7ed9a8609cc&code=" +
                                Request.Form["code"]),
                   token = HttpUtility.ParseQueryString(result)["access_token"];
            if (string.IsNullOrEmpty(token))
            {
                WriteLine("<div>连接失败！错误信息：</div>{1}<pre>{0}</pre>", result, Environment.NewLine);
                return;
            }
            WriteLine("<div>尝试获取当前用户名中……</div>");
            Response.Flush();
            var match = Regex.Match(result = Api("https://appharbor.com/user", token: token,
                                                 method: "GET"), "\"username\": \"(.*?)\"");
            if (!match.Success)
            {
                WriteLine("<div>获取用户名失败！错误信息：</div>{1}<pre>{0}</pre>", result, Environment.NewLine);
                return;
            }
            if (string.IsNullOrWhiteSpace(Request.Form["redeploy"]))
            {
                WriteLine("<div>创建应用中……</div>");
                Response.Flush();
                result = Api("https://appharbor.com/applications",
                             string.Format("name={0}&region_identifier={1}", name, Request.Form["region"]), token);
                if (!string.IsNullOrEmpty(result) || uri == null
                    || !uri.StartsWith("https://appharbor.com/applications/", true, CultureInfo.InvariantCulture))
                {
                    WriteLine("<div>创建失败！{0}</div>{1}",
                        string.IsNullOrWhiteSpace(result) ? string.Empty : "错误信息:",
                        string.IsNullOrWhiteSpace(result) ? string.Empty : "<pre>" + result + "</pre>");
                    return;
                }
                name = uri.Substring(35);
                WriteLine("<div>初始化配置中……</div>");
                Response.Flush();
                Api(string.Format("https://appharbor.com/applications/{0}/filesystemwriteacces", name),
                    string.Empty, token);
                Api(string.Format("https://appharbor.com/applications/{0}/precompilation", name),
                    "_method=delete", token);
            }
            WriteLine("<div>推送<a href=\"https://github.com/Mygod/Skylark/\">邪恶的代码</a>中……</div>");
            Response.Write("<pre>");
            Response.Flush();
            using (var repo = new Repository(Repository.Init(dir)))
            {
                repo.Index.Stage(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                                          .Select(path => path.Substring(dir.Length))
                                          .Where(path => !path.StartsWith(".git", true, CultureInfo.InvariantCulture)),
                                          new ExplicitPathsOptions
                                          {
                                              OnUnmatchedPath = msg =>
                                              {
                                                  WriteLine("git add: {0}", msg);
                                                  Response.Flush();
                                              }
                                          });
                repo.Commit("Skylark Deployer Commit");
                var remoteName = "appharbor";
                if (repo.Network.Remotes[remoteName] != null)
                {
                    var i = 0;
                    while (repo.Network.Remotes[remoteName + i] != null) i++;
                    remoteName += i;
                }
                var remote = repo.Network.Remotes.Add(remoteName,
                    string.Format("https://{0}@appharbor.com/{1}.git", match.Groups[1].Value, name));
                repo.Network.Push(remote,
                                  remote.RefSpecs.Select(refSpec => refSpec.Specification.Replace("/*", "/master")),
                                  new PushOptions
                {
                    Credentials = new Credentials
                        { Username = match.Groups[1].Value, Password = Request.Form["password"] },
                    OnPackBuilderProgress = (stage, current, total) =>
                    {
                        WriteLine("git push Pack Builder Progress: {0}/{1}, Stage: {2}", current, total, stage);
                        Response.Flush();
                        return true;
                    },
                    OnPushStatusError = errors =>
                    {
                        WriteLine("git push Status Error: {0} ({1})", errors.Message, errors.Reference);
                        Response.Flush();
                    },
                    OnPushTransferProgress = (current, total, bytes) =>
                    {
                        WriteLine("git push Transfer Progress: {0}/{1}, Bytes: {2}", current, total, bytes);
                        Response.Flush();
                        return true;
                    }
                });
            }
            WriteLine("</pre>");
            WriteLine("<div>部署完毕。请等待 1 分钟，然后点击<a href=\"http://{0}.apphb.com/Update/\" " +
                      "target=\"_blank\">这里</a>开始更新你的站点，更新完成后点击<a " +
                      "href=\"http://{0}.apphb.com/View/readme.htm\" target=\"_blank\">这里</a>" +
                      "进入你崭新的 云雀™。（实验说明开始更新后 15 秒左右更新就完成了）</div>", name);
            WriteLine("<div>完成后您也可以前往 <a href=\"https://appharbor.com/applications/{0}\" target=\"_blank\">" +
                      "AppHarbor</a> 删除您的应用或对您的应用进行更多配置。</div>");
        }

        private string uri;
        private string Api(string address, string data = null, string token = null, string method = "POST")
        {
            var request = (HttpWebRequest)WebRequest.Create(address);
            request.AllowAutoRedirect = false;
            request.Method = method;
            request.Accept = "application/json";
            request.ContentType = "application/x-www-form-urlencoded";
            if (token != null) request.Headers["Authorization"] = "BEARER " + token;
            if (data != null)
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                request.ContentLength = bytes.LongLength;
                using (var stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
            }
            using (var response = request.GetResponse())
            {
                uri = response.Headers["Location"];
                using (var stream = response.GetResponseStream()) return new StreamReader(stream).ReadToEnd();
            }
        }

        private void WriteLine(string format, params object[] args)
        {
            Response.Write(String.Format(format, args) + Environment.NewLine);
        }
    }
}