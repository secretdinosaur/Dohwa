#if UNITY_EDITOR

using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEngine;

public static class LocalizationGoogleSheetsPullTool
{
    // 유니티 상단 메뉴에 실행 버튼을 생성합니다.
    [MenuItem("Tools/Localization/Pull All Google Sheets")]
    public static void PullAllStringTables()
    {
        // 1. 프로젝트 내의 모든 String Table Collection을 가져옵니다.
        var collections = LocalizationEditorSettings.GetStringTableCollections().ToList();

        if (collections.Count == 0)
        {
            return;
        }

        int successCount = 0;
        int skipCount = 0;

        try
        {
            for (int i = 0; i < collections.Count; i++)
            {
                var collection = collections[i];

                // 2. Collection에 등록된 GoogleSheetsExtension을 찾습니다.
                var sheetsExtension = collection.Extensions.OfType<GoogleSheetsExtension>().FirstOrDefault();

                // Google Sheets 연결이 없거나 Provider 설정이 누락된 경우 건너뜁니다.
                if (sheetsExtension == null || sheetsExtension.SheetsServiceProvider == null)
                {
                    skipCount++;
                    continue;
                }

                // 진행 상태 프로그레스 바 표시 (대량 작업 시 에디터가 멈춘 것처럼 보이는 것 방지)
                EditorUtility.DisplayProgressBar(
                    "Pulling Localization Data",
                    $"[{i + 1}/{collections.Count}] Pulling '{collection.TableCollectionName}' from Google Sheets...",
                    (float)i / collections.Count
                );

                // 3. Google Sheets 서비스 객체 생성 및 스프레드시트 ID 할당
                var googleSheets = new GoogleSheets(sheetsExtension.SheetsServiceProvider)
                {
                    SpreadSheetId = sheetsExtension.SpreadsheetId
                };

                // 4. 데이터 Pull 실행 (핵심 API)
                googleSheets.PullIntoStringTableCollection(
                    sheetsExtension.SheetId,
                    collection,
                    sheetsExtension.Columns
                );

                // 에셋이 수정되었음을 유니티에 알림
                EditorUtility.SetDirty(collection);
                successCount++;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Localization Pull] 작업 중 오류가 발생했습니다: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            // 작업 중단이나 완료 시 반드시 프로그레스 바를 닫아줍니다.
            EditorUtility.ClearProgressBar();

            // 변경된 String Table Collection 에셋들을 디스크에 최종 저장합니다.
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"<b>[Localization Pull] 일괄 작업 완료!</b> (성공: {successCount}개 / 건너뜀: {skipCount}개)");
    }
}
#endif