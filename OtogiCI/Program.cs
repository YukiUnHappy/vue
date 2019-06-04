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

            if (!match.Success)
                throw new Exception("The server maybe under maintenance");

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

            if (l.Count < 4)
                throw new Exception("Mismatch the count, maybe something change?");

            var nsb = new StringBuilder();

            var rlp = new List<KeyValuePair<int, int>>();
            var rls = new List<string>();

            foreach (var i in l)
            {
                if (i == p)  //Old HScene Spine Load (Still::SetStill)
                {
                    var ii = p - 1000;

                    //Find out the start of .Still::SetStill(System.Action ac)
                    var pFunc = BoyerMooreHorspool.Find(js, "function ", ii);
                    while (pFunc < i)
                    {
                        var te = BoyerMooreHorspool.Find(js, "function ", pFunc + 1);
                        if (te > i)
                            break;
                        pFunc = te;
                    }

                    //Find out the variables
                    var pVar = BoyerMooreHorspool.Find(js, "var ", pFunc + 1);

                    //Insert two variables
                    rlp.Add(new KeyValuePair<int, int>(pVar + 4, 0));
                    rls.Add("m=0,n=0,");


                    //Find out the if(Application.isEditor && ...
                    var pIf = BoyerMooreHorspool.Find(js, "if(", pFunc + 1);
                    while (pIf < i)
                    {
                        var te = BoyerMooreHorspool.Find(js, "if(", pIf + 1);
                        if (te > i)
                            break;
                        pIf = te;
                    }

                    if (js.Substring(pIf + 6, 9) != "(0,0)|0?(")
                        throw new Exception("Pattern A mismatch");

                    //Replace true
                    rlp.Add(new KeyValuePair<int, int>(pIf + 3, 8));
                    rls.Add("1");


                    //Find Hack Point
                    ii = BoyerMooreHorspool.Find(js, k, pIf + 1);  //f3D(0,YUx(G_x(w0D(h,0)|0,0)|0,0)|0,0)|0,0);

                    var o = js.Substring(ii + 14, 8);  //w0D(h,0)

                    //Insert hack
                    rlp.Add(new KeyValuePair<int, int>(ii - 6, 0));
                    rls.Add($"m={k2}{o}|0,0)|0,0)|0;if(!(n|0)?(c[m+8>>2]|0)==14:0)n=m;if(n|0?(c[m+8>>2]|0)==15:0)m=n;");

                    /*
                     * if (keep == null && str.Length == 14) //Spine/Skeleton
                     *      keep = str;
                     * if (keep != null && str.Length == 15) //Ist/MosaicField
                     *      str = keep;
                     */

                    if (js.Substring(ii + 6 + 26, 5) != "|0,0)")
                        throw new Exception("Pattern B mismatch");

                    //Replace m
                    rlp.Add(new KeyValuePair<int, int>(ii + 6, 26));
                    rls.Add("m");
                }
                else  //New HScene Spine Load (ResourceManager::LoadCacheGameEffect,ResourceManager::LoadGameEffect,ResourceManager::LoadGameSpine)
                {
                    var h = js.Substring(i - 6, 6);  //H_x(b,
                    var o =
                        $"var d=0,e=0;d={k2}b,0)|0,0)|0;if((f[d+8>>2]|0)==20){{f[d+8>>2]=14;e=1;}}{h}{k1}0,d|0,0)|0,0);if(e|0)f[d+8>>2]=20;";

                    /*
                     * if (str.Length == 20) { //Spine/SkeletonMosaic
                     *      str.Length = 14;   //Spine/Skeleton
                     *      changed = true;
                     * }
                     * DoSomething();
                     * if (changed)
                     *      str.Length = 20;
                     */

                    rlp.Add(new KeyValuePair<int, int>(i - 6, 40));  //H_x(b,f3D(0,YUx(G_x(b,0)|0,0)|0,0)|0,0);
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
                using (var gzip = new GZipStream(ret, CompressionLevel.Optimal))
                {
                    new MemoryStream(data).CopyTo(gzip);
                }

                return ret.ToArray();
            }
        }
    }
}
