using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://myproject-76240-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text AntidoteCountText;
    [SerializeField] Text DegassingCountText;
    [SerializeField] Text SpannerCountText;
    [SerializeField] Text ShotGunCountText;
    [SerializeField] Text Unit1Text;
    [SerializeField] Text Unit2Text;
    [SerializeField] Text Unit3Text;
    [SerializeField] Text Unit4Text;
    [SerializeField] Text Unit5Text;
    [SerializeField] Text Unit6Text;
    [SerializeField] Text MessageText;

    [SerializeField] string NextSceneName = "MainScene";
    [SerializeField] string GameSceneName = "GameScene";

    string userKey;
    Dictionary<string, int> inventory = new Dictionary<string, int>();
    Dictionary<string, bool> unitList = new Dictionary<string, bool>();

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            MessageText.text = "로그인 정보가 없습니다.";
            return;
        }

        LoadInventory();
    }

    public void MoveStoreScene()
    {
        SceneManager.LoadScene(NextSceneName);
    }

    public void MoveGameScene()
    {
        SceneManager.LoadScene(GameSceneName);
    }

    void LoadInventory()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "인벤토리 불러오기 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (!snapshot.Exists)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "유저 데이터가 없습니다.";
                    });
                    return;
                }

                if (snapshot.Child("Inventory").Value != null)
                {
                    string inventoryJson = snapshot.Child("Inventory").Value.ToString();
                    inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);
                }
                else
                {
                    inventory = new Dictionary<string, int>();
                }

                if (snapshot.Child("UnitList").Value != null)
                {
                    string unitListJson = snapshot.Child("UnitList").Value.ToString();
                    unitList = JsonConvert.DeserializeObject<Dictionary<string, bool>>(unitListJson);
                }
                else
                {
                    unitList = new Dictionary<string, bool>();
                }

                dispatcher.Enqueue(() =>
                {
                    InvenRefreshUI();
                    UnitRefreshUI();
                    MessageText.text = "인벤토리 불러오기 완료";
                });
            });
    }

    void InvenRefreshUI()
    {
        AntidoteCountText.text = "Antidote : " + GetItemCount("Antidote");
        DegassingCountText.text = "Degassing : " + GetItemCount("Degassing");
        SpannerCountText.text = "Spanner : " + GetItemCount("Spanner");
        ShotGunCountText.text = "ShotGun : " + GetItemCount("ShotGun");
    }

    int GetItemCount(string itemName)
    {
        if (inventory.ContainsKey(itemName))
        {
            return inventory[itemName];
        }

        return 0;
    }

    public void OnClickUseAntidote()
    {
        UseItem("Antidote");
    }

    public void OnClickUseDegassing()
    {
        UseItem("Degassing");
    }

    public void OnClickUseSpanner()
    {
        UseItem("Spanner");
    }

    public void OnClickUseShotGun()
    {
        UseItem("ShotGun");
    }

    void UseItem(string itemName)
    {
        if (!inventory.ContainsKey(itemName) || inventory[itemName] <= 0)
        {
            MessageText.text = itemName + " 개수가 부족합니다.";
            return;
        }

        inventory[itemName]--;
        SaveInventory(itemName);
    }

    void SaveInventory(string usedItemName)
    {
        string inventoryJson = JsonConvert.SerializeObject(inventory);

        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Inventory")
            .SetValueAsync(inventoryJson)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "인벤토리 저장 실패";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    InvenRefreshUI();
                    MessageText.text = usedItemName + " 사용 완료";
                });
            });
    }

    void UnitRefreshUI()
    {
        Unit1Text.text = "Unit1 : " + GetUnitStateText("Unit1");
        Unit2Text.text = "Unit2 : " + GetUnitStateText("Unit2");
        Unit3Text.text = "Unit3 : " + GetUnitStateText("Unit3");
        Unit4Text.text = "Unit4 : " + GetUnitStateText("Unit4");
        Unit5Text.text = "Unit5 : " + GetUnitStateText("Unit5");
        Unit6Text.text = "Unit6 : " + GetUnitStateText("Unit6");
    }

    string GetUnitStateText(string unitName)
    {
        if (unitList.ContainsKey(unitName) && unitList[unitName])
        {
            return "보유";
        }

        return "미보유";
    }
}
