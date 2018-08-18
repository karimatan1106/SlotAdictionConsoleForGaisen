using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlotAddictionCore.Models;
using SlotAddictionLogic.Line;
using SlotAddictionLogic.Models;

namespace SlotAddictionConsoleForGaisen
{
    public class Program
    {
        #region フィールド
        /// <summary>
        /// ライン通知のトークン
        /// </summary>
        private static string _lineToken = "Stjd5Laa2ZPH6tZ6OIxpOJtxR9PbVMxt8kBC9Sa4OYU";

        /// <summary>
        /// テキストファイル名
        /// </summary>
        private const string TextFileName = "PreResult.txt";

        #endregion

        #region メソッド

        private static async Task Main(string[] args)
        {
            var date = DateTime.Now;
            //データが更新されない時間帯は取得を避けておく
            if (date.Hour < 10
                || date.Hour >= 22)
            {
#if DEBUG
                date = date.AddDays(-1);
#elif !DEBUG
                return;
#endif
            }

            //念のため
            try
            {
                var (Output, OutputUri) = await GetAnalyseResult(date);
                if (OutputUri.Any())
                {
                    //前回情報を取得する
                    if (!File.Exists(TextFileName))
                    {
                        using (File.Create(TextFileName)) { }
                    }

                    //前回抽出結果に含まれていないURLがあれば出力する
                    var sr = File.ReadAllLines(TextFileName);
                    if (OutputUri.Except(sr).Any())
                    {
#if DEBUG
                        _lineToken = "XLzPrFUKoS33P4QCAqO15M31c9RfDhYMJjgJoV5fIJX";
#endif
                        LineAlert.Message(_lineToken, Output);
                        File.Delete(TextFileName);
                        using (File.Create(TextFileName)) { }
                        File.AppendAllLines(TextFileName, OutputUri);
                    }
                }
            }
            catch (Exception e)
            {
                var hoge = 0;
                //ignore
            }

#if !DEBUG
            var folderPath = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            var folderName = Path.GetFileName(folderPath);
            _lineToken = "XLzPrFUKoS33P4QCAqO15M31c9RfDhYMJjgJoV5fIJX";
            LineAlert.Message(_lineToken, folderName + Environment.NewLine + "動作してるよ");
#endif
        }
        /// <summary>
        /// 遊技履歴から狙い目台の情報解析を行う
        /// </summary>
        /// <param name="searchDate"></param>
        /// <returns></returns>
        private static async Task<(string Output, List<string> OutputUri)> GetAnalyseResult(DateTime searchDate)
        {
            var output = new StringBuilder(string.Empty);
            var outputUri = new List<string>();

            var tenpoInfo = ExcelData.GetTenpoInfo();
            var slotMachineSearchNames = ExcelData.GetSlotMachineSearchNames();

            var analyseSlotData = new AnalyseSlotData(searchDate);
            foreach (var slotMachineSearchName in slotMachineSearchNames)
            {
                //機種名を検索する文字列を取得して解析する
                var aimMachineInfo = ExcelData.GetAimMachineInfo().Where(x => x.MachineName == slotMachineSearchName.Key).ToList();
                await analyseSlotData.GetSlotDataAsync(tenpoInfo, slotMachineSearchName.Value, aimMachineInfo);

                //狙える台がない場合
                if (!analyseSlotData.SlotPlayDataCollection.Any())
                {
                    continue;
                    output.AppendLine("(設置されて)ないです。");
                }
                else if (analyseSlotData.AimMachineCollection.Any())
                {
                    //機種名を出力
                    output.AppendLine(Environment.NewLine + "【" + slotMachineSearchName.Key + "】");

                    //ステータスでグルーピングする
                    var aimMachineStatusGrouping = analyseSlotData.AimMachineCollection
                        .OrderByDescending(x => x.CoinPrice) //コレクションの中身をコイン単価で降順に並べる
                        .ThenByDescending(x => x.StartCount) //コレクションの中身をスタート数で降順に並べる
                        .GroupBy(x => new
                        {
                            x.Status,
                            x.ExceptValue,
                        }) //ステータスと期待値でグループ化する
                        .OrderByDescending(x => x.Key.ExceptValue); //グループを期待値で降順に並べる
                    foreach (var aimMachines in aimMachineStatusGrouping)
                    {
                        output.AppendLine("[" + aimMachines.Key.Status + "]");

                        foreach (var aimMachine in aimMachines)
                        {
                            output.AppendLine("(" + aimMachine.CoinPrice + "円) " + aimMachine.StoreName + " " + aimMachine.StartCount + "G");
                            output.AppendLine(aimMachine.SlotMachineUri.ToString());
                            outputUri.Add(aimMachine.SlotMachineUri.ToString());
                        }
                    }
                }
                else
                {
                    continue;
                    output.AppendLine("狙える台は無いンゴなぁ");
                }
            }

            output.AppendLine();
            return (output.ToString(), outputUri.OrderBy(x => x).ToList());
        }
        #endregion
    }
}
