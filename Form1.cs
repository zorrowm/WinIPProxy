using AngleSharp.Dom;
using AngleSharp;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using XFrameBack.Log;
using Flurl.Util;
using Flurl.Http;
using System.Collections.Concurrent;
using System.Windows.Forms;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using XFrameBack.Utils;
using AngleSharp.Io;
using System.Drawing;

namespace WinIPProxy
{
    public partial class Form1 : Form
    {
        private string testURL = "https://testproxy.ysgis.com/test";
        private string testURL2 = "https://testproxy.ysgis.com/test/GetTile";
        private string defaultURLTemplate = "http://www.66ip.cn/areaindex_1/{0}.html";
        private ConcurrentBag<IPAddress> bagCollection = new ConcurrentBag<IPAddress>();
        private ConcurrentBag<IPAddress> bagCollection2 = new ConcurrentBag<IPAddress>();
        public Form1()
        {
            InitializeComponent();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            string urlTemplate = this.textBox1.Text;
            for (int i = 1; i <= 10; i++)
            {
                string url=string.Format(urlTemplate, i);
                await filterIPPort(url, bagCollection);
            }
       

            MessageBox.Show("过滤完成！");
        }

        //将GB2312编码转换成汉字
        private string BytesToString(byte[] bytes)
        {
            string myString;
            Encoding fromEcoding = Encoding.ASCII;//GetEncoding("UTF-8");
            Encoding toEcoding = Encoding.GetEncoding("GBK");//gb2312UTF-8
            byte[] toBytes = Encoding.Convert(fromEcoding, toEcoding, bytes);
            myString = toEcoding.GetString(toBytes);//将字节数组解码成字符串
            return myString;
        }
        private async Task filterIPPort(string url, ConcurrentBag<IPAddress>  bagCollection)
        {
            string htmlContent = "";
            try
            {
                htmlContent = await url.WithHeader("Content-Type", "text/html; charset=gb2312").GetStringAsync();
               // htmlContent=BytesToString(bytes);
                //StreamReader reader = new StreamReader(stream, Encoding.GetEncoding("gb2312"));
                //htmlContent = reader.ReadToEnd();
                //reader.Close();
            }
            catch (Exception ex)
            {
                SystemLogger.getLogger().Warn("抓取网页：" + url, ex);
            }
            if (string.IsNullOrEmpty(htmlContent))
                return ;
            var ipPortList =await dealIPPortAsync(htmlContent);
            if(ipPortList!=null)
            ipPortList.ForEach(async it =>
            {

               var result =await Task.Run<bool>(() =>
                {
                    return valid(it.IP, it.Port);
                });
                if (result==true)
                { //TODO:更新界面，并记录当前IPAddress
                    if (bagCollection.Contains(it))
                        return;
                    this.richTextBox1.Text += $"{it.IP}:{it.Port};\r\n";
                    bagCollection.Add(it);

                    var result2 = await Task.Run<bool>(() =>
                    {
                        return validBytesAsync(it.IP, it.Port);
                    });
                    if (result2)
                    {
                        this.richTextBox2.Text += $"{it.IP}:{it.Port};\r\n";
                        bagCollection2.Add(it);
                    }
                    
                }
            });

        }

        /// <summary>
        /// 获取IPAddress列表
        /// </summary>
        /// <param name="htmlContent">网页内容</param>
        /// <returns></returns>
        private async Task<List<IPAddress>> dealIPPortAsync(string htmlContent)
        {
            try
            {
                AngleSharp.IConfiguration config = Configuration.Default;
                //Create a new context for evaluating webpages with the given config
                IBrowsingContext context = BrowsingContext.New(config);
                IDocument document = await context.OpenAsync(req => req.Content(htmlContent));
                var d1Collection = document.QuerySelectorAll("table[width='100%'] tr");
                List<IPAddress> nameList = new List<IPAddress>();
                int i = 0;
                foreach (var item in d1Collection)
                {
                    if (i > 0)
                    {
                        var aCollection = item.Children;

                        string ip=aCollection[0].InnerHtml;
                        string port= aCollection[1].InnerHtml;
                        var ipaddress = new IPAddress()
                        {
                            IP = ip,
                            Port = int.Parse(port)
                        };
                        if(!nameList.Contains(ipaddress))
                        nameList.Add(ipaddress);
                    }
                    i++;
               
                }
                return nameList;

            }
            catch (Exception ex)
            {
                SystemLogger.getLogger().Warn("解析IPAddress", ex);
            }
            return null;
        }
        /// <summary>
        /// 测试IP Proxy
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        private bool valid(string ip, int port)
        {
            System.Net.WebProxy proxyObject = new System.Net.WebProxy(ip, port);//str为IP地址 port为端口号 代理类
            System.Net.HttpWebRequest Req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(testURL); // 访问这个网站 ，返回的就是你发出请求的代理ip 这个做代理ip测试非常方便，可以知道代理是否成功

            Req.UserAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; QQWubi 133; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; CIBA; InfoPath.2)";

            Req.Proxy = proxyObject; //设置代理 
            Req.Method = "GET";
            Req.Timeout = 60000;//60秒
            HttpWebResponse Resp = null;
            Encoding code = Encoding.Default;
            string returns = "";
            try
            {
                Resp = (HttpWebResponse)Req.GetResponse();
                using (System.IO.StreamReader sr = new System.IO.StreamReader(Resp.GetResponseStream(), code))
                {
                    returns = sr.ReadToEnd();
                }
                if ("OK".Equals(returns))
                    return true;
                else
                 return false;
            }
            catch (Exception ex)
            {
                //输出日志
                return false;
            }
            finally
            {
                if (Resp != null)
                {
                    Resp.Close();
                    Resp.Dispose();
                }
            }
     
  
        }

        private async Task<bool> validBytesAsync(string ip, int port)
        {
            System.Net.WebProxy proxyObject = new System.Net.WebProxy(ip, port);//str为IP地址 port为端口号 代理类
            System.Net.HttpWebRequest Req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(testURL2); // 访问这个网站 ，返回的就是你发出请求的代理ip 这个做代理ip测试非常方便，可以知道代理是否成功

            Req.UserAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; QQWubi 133; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; CIBA; InfoPath.2)";

            Req.Proxy = proxyObject; //设置代理 
            Req.Method = "GET";
            Req.Timeout = 60000;//60秒
            HttpWebResponse Resp = null;
            Encoding code = Encoding.Default;
  
            try
            {
                Resp = (HttpWebResponse)Req.GetResponse();
                var bytes = default(byte[]);
                using (System.IO.StreamReader reader = new System.IO.StreamReader(Resp.GetResponseStream(), code))
                {
                    using (var memstream = new MemoryStream())
                    {
                        reader.BaseStream.CopyTo(memstream);
                        bytes = memstream.ToArray();
                    }
                }
                //char[] buffer;
                //using (var sr = new StreamReader(Resp.GetResponseStream()))
                //{
                //    long count = sr.BaseStream.Length;
                //    buffer = new char[count];
                //    await sr.ReadAsync(buffer, 0, (int)sr.BaseStream.Length);
                //}
                //StreamReader read = new StreamReader(stream);
                //var len=read.BaseStream.Length;
                //read.Close(); 
                //var returns= StreamToBytes(stream);
                if (bytes!=null&& bytes.Length == 2611)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                //输出日志
                return false;
            }
            finally
            {
                if (Resp != null)
                {
                    Resp.Close();
                    Resp.Dispose();
                }
            }


        }

        private byte[] StreamToBytes(Stream stream)
        {
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            // 设置当前流的位置为流的开始
            stream.Seek(0, SeekOrigin.Begin);
            return bytes;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.textBox1.Text = defaultURLTemplate;
        }

        private void button2_Click(object sender, EventArgs e)
        {
           var list= bagCollection.ToList();
           string result=JsonHelper.SerializeObject(list);
            string filePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ipList.txt");
            FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate);
            using StreamWriter wr = new StreamWriter(fs);
            wr.Write(result);
            wr.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var list = bagCollection2.ToList();
            string result = JsonHelper.SerializeObject(list);
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ipList2.txt");
            FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate);
            using StreamWriter wr = new StreamWriter(fs);
            wr.Write(result);
            wr.Close();
        }
    }
}