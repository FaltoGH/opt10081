using AxKHOpenAPILib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace opt10081
{
    internal class Program
    {
        static long tolong(object x)
        {
            return long.Parse(x.ToString());
        }

        class csvrow : baserow
        {
            // 종목코드
            public string jmcode;

            public csvrow(baserow x, string jmcode)
            {
                close = x.close;
                volume = x.volume;
                volmoney = x.volmoney;
                open = x.open;
                high = x.high;
                low = x.low;
                mtype = x.mtype;
                mratio = x.mratio;

                this.jmcode = jmcode;
            }

            static string escape(string x)
            {
                // This follows RFC 4180.
                // A -> "A"
                // A"S -> "A""S"
                // A,S -> "A,S"
                // A S -> "A S"

                x = x.Replace("\"", "\"\"");
                return '"' + x + '"';
            }

            public override string ToString()
            {
                return string.Join(",", new object[] { escape(jmcode), close, volume, volmoney, open, high, low, escape(mtype), mratio });
            }
        }

        class chartrow : baserow
        {
            // 일자(yyyyMMdd)
            public string date;

            public static chartrow get(object[,] dataex, int i)
            {
                string s(int x)
                {
                    return dataex[i, x].ToString();
                }
                long t(int x)
                {
                    return tolong(dataex[i, x]);
                }

                var ret = new chartrow();
                ret.close = t(1);
                ret.volume = t(2);
                ret.volmoney = t(3);

                // yyyyMMdd
                ret.date = s(4);
                if(ret.date.Length != 8)
                {
                    exit2("date="+ret.date);
                    return null;
                }

                ret.open = t(5);
                ret.high = t(6);
                ret.low = t(7);
                ret.mtype = s(8);
                ret.mratio = float.Parse(s(9));

                return ret;
            }

            public static chartrow[] get2(object[,] dataex)
            {

                int nrow = dataex.GetLength(0);

                var ret = new chartrow[nrow];

                for (int i = 0; i < nrow; i++)
                    ret[i] = chartrow.get(dataex, i);

                return ret;
            }
        }

        class baserow
        {
            // 현재가
            public long close;

            // 거래량
            public long volume;

            // 거래대금
            public long volmoney;

            // 시가
            public long open;

            // 고가
            public long high;

            // 저가
            public long low;

            // 수정주가구분
            public string mtype;

            // 수정비율
            public float mratio;
        }

        volatile static AxKHOpenAPI api;
        static AxKHOpenAPI newapi()
        {
            var a = new AxKHOpenAPI();
            new Control().Controls.Add(a);
            a.EndInit();
            return a;
        }

        static void oncommconnect()
        {
            while (api != null && !api.IsDisposed)
            {
                Thread.Sleep(1);
                Application.DoEvents();
            }
        }

        static readonly char[] semicl = new char[1] { ';' };
        static HashSet<string> codeset(string market)
        {
            return api.GetCodeListByMarket(market).Split(semicl, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        }

        static bool isspac(string jmcode)
        {
            return api.GetMasterCodeName(jmcode).Contains("스팩");
        }

        static string stockmarketkind(string jmcode)
        {
            return api.KOA_Functions("GetStockMarketKind", jmcode);
        }

        static bool is_etn(string jmcode)
        {
            return stockmarketkind(jmcode) == "60";
        }

        static bool isnormal(string jmcode)
        {
            return jmcode[5] == '0';
        }

        static string ordwrn(string jmcode)
        {
            return api.KOA_Functions("IsOrderWarningStock", jmcode);
        }

        static bool cleaning(string jmcode)
        {
            return ordwrn(jmcode) == "2";
        }

        static HashSet<string> normal()
        {
            var target = new HashSet<string>();
            var kospi = codeset("0");
            var kosdaq = codeset("10");
            var elw = codeset("3");
            var etf = codeset("8");
            var konex = codeset("50");
            var mutualfund = codeset("4");
            var warrant = codeset("5");
            var ritz = codeset("6");
            var highyieldfund = codeset("9");
            var kotc = codeset("30");

            target.UnionWith(kospi);
            target.UnionWith(kosdaq);

            target.ExceptWith(elw);
            target.ExceptWith(etf);
            target.ExceptWith(konex);
            target.ExceptWith(mutualfund);
            target.ExceptWith(warrant);
            target.ExceptWith(ritz);
            target.ExceptWith(highyieldfund);
            target.ExceptWith(kotc);

            // 보통주만 남기기
            target.RemoveWhere(x => !isnormal(x));

            // 스팩주 제외
            target.RemoveWhere(isspac);

            // ETN 제외
            target.RemoveWhere(is_etn);

            // 정리매매 제외
            target.RemoveWhere(cleaning);

#if DEBUG
            // TODO: N=Size of target. Remove N-101 elements randomly.
#endif

            return target;
        }

        static string newrqn()
        {
            return Guid.NewGuid().ToString("X").Substring(0, 6);
        }

        static byte __scrno;
        static string newscr()
        {
            __scrno++;
            if(__scrno > 9)
            {
                __scrno = 0;
            }
            return "100" + __scrno;
        }

        static void exit()
        {
            api.Dispose();
            api = null;
            mainth.Abort();
            Application.Exit();
            Environment.Exit(0);
            Process.GetCurrentProcess().Kill();
        }

        static void exit2(object reason)
        {
            Console.Error.WriteLine(reason);
            exit();
        }

        // crdict[yyyyMMdd] = yyyyMMdd's daily chart data of all jongmoks
        static readonly Dictionary<string, List<csvrow>> crdict = new Dictionary<string, List<csvrow>>();

        static string[] get3pieces(string yyyyMMdd)
        {
            return new string[] { yyyyMMdd.Substring(0, 4), yyyyMMdd.Substring(4, 2), yyyyMMdd.Substring(6, 2) };
        }

        static string getcsvfilename(string yyyyMMdd)
        {
            var threepieces = get3pieces(yyyyMMdd);
            int[] threeints = Array.ConvertAll(threepieces, int.Parse);
            return Path.Combine(datadir, threeints[0].ToString(), threeints[1].ToString(), threeints[2].ToString() + ".csv");
        }

        static void save2(string path, List<csvrow> csvrows)
        {
            var contents = csvrows.ConvertAll(x => x.ToString());
            File.WriteAllLines(path, contents);
        }

        static void save()
        {
            foreach(var kvpair in crdict)
            {

                string path = getcsvfilename(kvpair.Key);
                save2(path, kvpair.Value);
            }
        }

        static void connected()
        {
            var target = normal();

            int n = target.Count;
            Console.WriteLine("target.Count=" + n);

            api.OnReceiveTrData += Api_OnReceiveTrData;

            int i = 0;
            foreach (var jmcode in target)
            {
                Console.WriteLine($"{jmcode} ({i}/{n})");

                api.SetInputValue("종목코드", jmcode);
                api.SetInputValue("수정주가구분", "0");
                string rqname = newrqn();
                string scrno = newscr();
                dataex = null;
                int ret = api.CommRqData(rqname, "opt10081", 0, scrno);
                if (ret != 0)
                {
                    exit2(ret);
                    return;
                }

                // wait until data arrives
                while(dataex == null)
                {
                    Thread.Sleep(1);
                    Application.DoEvents();
                }

                // process data
                var chartrows = chartrow.get2(dataex);
                foreach (var crow in chartrows)
                {
                    if (!crdict.ContainsKey(crow.date))
                    {
                        crdict[crow.date] = new List<csvrow>();
                    }
                    crdict[crow.date].Add(new csvrow(crow, jmcode));
                }

                // free data
                dataex = null;

                if (i % 100 == 99)
                {
                    api = newapi();
                    api.OnReceiveTrData += Api_OnReceiveTrData;

                    Console.WriteLine("new api initialized");
                }

                // Wait 200ms to get idle
                Thread.Sleep(200);

                i++;
            }

            save();

            api.Dispose();
            api = null;
        }

        volatile static object[,] dataex;

        private static void Api_OnReceiveTrData(object sender, _DKHOpenAPIEvents_OnReceiveTrDataEvent e)
        {
            object commdataex = api.GetCommDataEx(e.sTrCode, e.sRecordName);
            object[,] commdataex2 = (object[,])commdataex;
            int nrow = commdataex2.GetLength(0);
            if(nrow <= 0)
            {
                exit2("data is empty");
                return;
            }



            Console.WriteLine("nrow=" + nrow);
            dataex = commdataex2;
        }

        private static void Api_OnEventConnect(object sender, _DKHOpenAPIEvents_OnEventConnectEvent e)
        {
            if (e.nErrCode == 0)
            {
                connected();
            }
        }



        volatile static Thread mainth;

        static string programdir;
        static string datadir;
        readonly static Random dice = new Random();

        [STAThread]
        static void Main(string[] args)
        {
            programdir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            datadir = Path.Combine(programdir, "..", "..", "..", "data");
            datadir = Path.GetFullPath(datadir);
            Directory.CreateDirectory(datadir);

            mainth = Thread.CurrentThread;
            api = newapi();
            api.OnEventConnect += Api_OnEventConnect;
            if (api.CommConnect() == 0)
                oncommconnect();
        }



    }
}
