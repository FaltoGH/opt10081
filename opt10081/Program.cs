using AxKHOpenAPILib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace opt10081
{
    internal class Program
    {
        static AxKHOpenAPI api;
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

        static void connected()
        {
            var target = normal();

            Console.WriteLine("target.Count=" + target.Count);

            api.OnReceiveTrData += Api_OnReceiveTrData;

            int i = 0;
            foreach (var jmcode in target)
            {
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

                // TODO: process data

                dataex = null;

                if (i % 100 == 99)
                {
                    api = newapi();
                    api.OnReceiveTrData += Api_OnReceiveTrData;
                }

                // Wait 200ms to get idle
                Thread.Sleep(200);

                i++;
            }
        }

        static object[,] dataex;

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



        static Thread mainth;



        [STAThread]
        static void Main(string[] args)
        {
            mainth = Thread.CurrentThread;
            api = newapi();
            api.OnEventConnect += Api_OnEventConnect;
            if (api.CommConnect() == 0)
                oncommconnect();
        }



    }
}
