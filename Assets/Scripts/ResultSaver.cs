using Firebase.Database;
using PimDeWitte.UnityMainThreadDispatcher;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultSaver : MonoBehaviour
{
    public static ResultSaver Instance;

    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [SerializeField] string databaseUrl = "https://exam-68eae-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [SerializeField] Text MessageText;

    [SerializeField] int clearRewardCoin = 300;
    [SerializeField] int randomScoreMin = 100;
    [SerializeField] int randomScoreMax = 3000;

    [SerializeField] string NextSceneName = "Inventory";

    string userKey;

    bool isSaving = false;
    bool isSaved = false;

    void Awake()
    {
        Instance = this;

        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");
    }

    public void OnClickSaveRandomResult()
    {
        int randomScore = UnityEngine.Random.Range(randomScoreMin, randomScoreMax + 1);

        SaveClearResult(randomScore);
    }

    public void SaveClearResult(int finalScore)
    {
        SaveGameResult(finalScore, clearRewardCoin);
    }

    public void MoveInvenScene()
    {
        SceneManager.LoadScene(NextSceneName);
    }

    public void SaveGameResult(int finalScore, int rewardCoin)
    {
        if (isSaving)
            return;

        if (string.IsNullOrEmpty(userKey))
        {
            SetMessage("로그인 정보가 없습니다.");
            return;
        }

        finalScore = Mathf.Max(0, finalScore);
        rewardCoin = Mathf.Max(0, rewardCoin);

        isSaving = true;
        SetMessage("게임 결과 저장 중...");

        DatabaseReference userRef = reference
            .Child("UserInfo")
            .Child(userKey);

        userRef.GetValueAsync().ContinueWith(loadTask =>
        {
            if (loadTask.IsFaulted || loadTask.IsCanceled)
            {
                dispatcher.Enqueue(() =>
                {
                    isSaving = false;
                    SetMessage("유저 정보 불러오기 실패");

                    if (loadTask.Exception != null)
                        Debug.LogError(loadTask.Exception);
                });
                return;
            }

            DataSnapshot snapshot = loadTask.Result;

            if (!snapshot.Exists)
            {
                dispatcher.Enqueue(() =>
                {
                    isSaving = false;
                    SetMessage("유저 데이터가 없음.");
                });
                return;
            }

            int currentCoin = GetInt(snapshot.Child("Coin").Value, 0);
            int bestScore = GetInt(snapshot.Child("Score").Value, 0);

            int newCoin = currentCoin + rewardCoin;
            int newBestScore = bestScore;

            if (finalScore > bestScore)
            {
                newBestScore = finalScore;
            }

            Dictionary<string, object> updateData = new Dictionary<string, object>();
            updateData["Coin"] = newCoin;
            updateData["Score"] = newBestScore;
            updateData["LastScore"] = finalScore;
            updateData["LastRewardCoin"] = rewardCoin;

            userRef.UpdateChildrenAsync(updateData).ContinueWith(saveTask =>
            {
                if (saveTask.IsFaulted || saveTask.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        isSaving = false;
                        SetMessage("게임 결과 저장 실패");

                        if (saveTask.Exception != null)
                            Debug.LogError(saveTask.Exception);
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    isSaving = false;
                    isSaved = true;

                    SetMessage(
                        "결과 저장 완료\n" +
                        "이번 점수 : " + finalScore + "\n" +
                        "획득 코인 : " + rewardCoin + "\n" +
                        "현재 코인 : " + newCoin + "\n" +
                        "최고 점수 : " + newBestScore
                    );
                });
            });
        });
    }

    int GetInt(object value, int defaultValue)
    {
        if (value == null)
            return defaultValue;

        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return defaultValue;
        }
    }
        
    void SetMessage(string message)
    {
        if (MessageText != null)
            MessageText.text = message;
    }
}
