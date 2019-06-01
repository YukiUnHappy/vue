using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Horspool;

namespace OtogiCI
{
    class Program
    {
        static void Main(string[] args)
        {
            var webClient = new WebClient();

            var html = webClient.DownloadString("http://otogi-api.trafficmanager.net/Content/Atom?adult=True");

            var match = Regex.Match(html, "codeUrl: \"(.+?Release(\\d+).+?)\",");
            var url = match.Groups[1].Value + "gz";
            var sVer = match.Groups[2].Value;

            Console.WriteLine($"Server Ver:{sVer}");

            var lVer = webClient.DownloadString("https://otogi.dmmowari.cf/ver");
            Console.WriteLine($"Local Ver:{lVer}");

            if (lVer == sVer)
            {
                Console.WriteLine("No need to update");
                new FileStream("Newest", FileMode.CreateNew);
                return;
            }

            var data = webClient.DownloadData(url);

            Console.WriteLine("Download finished");

            //var data = File.ReadAllBytes("webGL.jsgz");

            data = Decompress(data);

            var js = Encoding.UTF8.GetString(data);

            js = Modify(js);

            Console.WriteLine("Modify finished");

            File.WriteAllText("webGL.js", js);
            //return;

            data = Compress(Encoding.UTF8.GetBytes(js));

            Directory.CreateDirectory($"Web\\Release{sVer}");

            File.WriteAllText($"Web\\ver", sVer);
            File.WriteAllBytes($"Web\\Release{sVer}\\webGL.jsgz", data);
        }

        private static string Modify(string js)
        {
            var p = BoyerMooreHorspool.Find(js, "=c[i+16+(f<<2)>>2]|0;");

            p += 40;

            var k = js.Substring(p, 14);

            var k1 = k.Substring(0, 4);
            var k2 = k.Substring(6, 8);

            var l = new List<int>();

            for (var i = 0; l.Count < 4 && i < js.Length; i++)
            {
                i = BoyerMooreHorspool.Find(js, k, i);

                if (i == -1)
                    break;

                l.Add(i);
            }

            var nsb = new StringBuilder();

            var rlp = new List<KeyValuePair<int, int>>();
            var rls = new List<string>();

            foreach (var i in l)
            {
                if (i == p)
                {

                }
                else
                {
                    var h = js.Substring(i - 6, 6);
                    var o =
                        $"var d=0,e=0;d={k2}b,0)|0,0)|0;if((f[d+8>>2]|0)==20){{f[d+8>>2]=14;e=1;}}{h}{k1}0,d|0,0)|0,0);if(e)f[d+8>>2]=20;";

                    rlp.Add(new KeyValuePair<int, int>(i - 6, 40));
                    rls.Add(o);
                }
            }

            var now = 0;
            for (var index = 0; index < rlp.Count; index++)
            {
                var point = rlp[index];
                if (now < point.Key)
                {
                    nsb.Append(js.Substring(now, point.Key - now));
                    now = point.Key;
                }

                nsb.Append(rls[index]);
                now += point.Value;
            }

            if (now < js.Length)
                nsb.Append(js.Substring(now, js.Length - now));

            return nsb.ToString();
        }

        static byte[] Decompress(byte[] data)
        {
            using (var ret = new MemoryStream())
            {
                using (var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
                {
                    gzip.CopyTo(ret);
                }

                return ret.ToArray();
            }
        }

        static byte[] Compress(byte[] data)
        {
            using (var ret = new MemoryStream())
            {
                using (var gzip = new GZipStream(ret, CompressionMode.Compress))
                {
                    new MemoryStream(data).CopyTo(gzip);
                }

                return ret.ToArray();
            }
        }
    }
}
