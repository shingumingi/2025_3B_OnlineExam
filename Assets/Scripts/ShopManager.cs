using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;
    InventoryManager inventoryManager;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://myproject-76240-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text CoinText;
    [SerializeField] Text MessageText;

    [SerializeField] string NextSceneName = "Inventory";

    string userKey;
    int currentCoin;
    Dictionary<string, int> inventory = new Dictionary<string, int>();
    Dictionary<string, bool> unitList = new Dictionary<string, bool>();

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();
        inventoryManager = GetComponent<InventoryManager>();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            MessageText.text = "로그인 정보가 없습니다.";
            return;
        }

        LoadUserData();
    }

    void LoadUserData()
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
                        MessageText.text = "유저 정보 불러오기 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                currentCoin = int.Parse(snapshot.Child("Coin").Value.ToString());

                string inventoryJson = snapshot.Child("Inventory").Value.ToString();
                inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);

                string unitJson = snapshot.Child("UnitList").Value.ToString();
                unitList = JsonConvert.DeserializeObject<Dictionary<string, bool>>(unitJson);

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = "유저 정보 불러오기 완료";
                });
            });
    }

    void RefreshUI()
    {
        CoinText.text = "Coin : " + currentCoin;
    }

    public void OnClickBuyAntidote()
    {
        BuyItem("Antidote", 150);
        RefreshUI();
    }

    public void OnClickBuyDegassing()
    {
        BuyItem("Degassing", 100);
        RefreshUI();
    }

    public void OnClickBuySpanner()
    {
        BuyItem("Spanner", 50);
        RefreshUI();
    }

    public void OnClickBuyShotGun()
    {
        BuyItem("ShotGun", 200);
        RefreshUI();
    }

    void BuyItem(string itemName, int price)
    {
        if (currentCoin < price)
        {
            MessageText.text = "코인이 부족합니다.";
            return;
        }

        currentCoin -= price;

        if (inventory.ContainsKey(itemName))
        {
            inventory[itemName]++;
        }
        else
        {
            inventory[itemName] = 1;
        }

        SaveUserData(itemName);
    }

    public void OnClickBuyUnit2()
    {
        BuyUnit("Unit2", 10);
    }

    public void OnClickBuyUnit3()
    {
        BuyUnit("Unit3", 20);
    }

    public void OnClickBuyUnit4()
    {
        BuyUnit("Unit4", 30);
    }

    public void OnClickBuyUnit5()
    {
        BuyUnit("Unit5", 40);
    }

    public void OnClickBuyUnit6()
    {
        BuyUnit("Unit6", 50);
    }

    void BuyUnit(string unitName, int price)
    {
        if (currentCoin < price)
        {
            MessageText.text = "코인이 부족합니다.";
            return;
        }

        if (unitList[unitName])
        {
            MessageText.text = "이미 보유한 유닛입니다.";
            return;
        }

        currentCoin -= price;
        unitList[unitName] = true;

        SaveUnitData(unitName);
    }

    void SaveUnitData(string boughtUnitName)
    {
        string unitJson = JsonConvert.SerializeObject(unitList);

        Dictionary<string, object> updateData = new Dictionary<string, object>();
        updateData["Coin"] = currentCoin;
        updateData["UnitList"] = unitJson;

        reference
            .Child("UserInfo")
            .Child(userKey)
            .UpdateChildrenAsync(updateData)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "구매 저장 실패";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = boughtUnitName + " 구매 완료";
                });
            });
    }

    public void MoveInvenScene()
    {
        SceneManager.LoadScene(NextSceneName);
    }

    void SaveUserData(string boughtItemName)
    {
        string inventoryJson = JsonConvert.SerializeObject(inventory);

        Dictionary<string, object> updateData = new Dictionary<string, object>();
        updateData["Coin"] = currentCoin;
        updateData["Inventory"] = inventoryJson;

        reference
            .Child("UserInfo")
            .Child(userKey)
            .UpdateChildrenAsync(updateData)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "구매 저장 실패";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = boughtItemName + " 구매 완료";
                });
            });
    }
}
