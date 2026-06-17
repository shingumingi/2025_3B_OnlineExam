using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AuctionManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://exam-68eae-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text CoinText;
    [SerializeField] Text InventoryText;
    [SerializeField] Text MessageText;

    [Header("Sell UI")]
    [SerializeField] Dropdown ItemDropdown;
    [SerializeField] InputField CountInput;
    [SerializeField] InputField PriceInput;

    [Header("Auction List UI")]
    [SerializeField] Transform Content;
    [SerializeField] AuctionItemUI AuctionItemPrefab;

    [Header("Scene")]
    [SerializeField] string InventorySceneName = "Inventory";

    string userKey;
    string nickName;

    int currentCoin;
    Dictionary<string, int> inventory = new Dictionary<string, int>();

    readonly string[] itemNames =
    {
        "Antidote",
        "Degassing",
        "Spanner",
        "ShotGun"
    };

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");
        nickName = PlayerPrefs.GetString("UserNickName");

        if (string.IsNullOrEmpty(userKey))
        {
            SetMessage("로그인 정보가 없습니다.");
            return;
        }

        if (string.IsNullOrEmpty(nickName))
            nickName = userKey;

        InitDropdown();

        LoadMyUserData();
        LoadAuctionItems();
    }

    void InitDropdown()
    {
        if (ItemDropdown == null)
            return;

        ItemDropdown.ClearOptions();
        ItemDropdown.AddOptions(new List<string>(itemNames));
    }

    public void LoadMyUserData()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("내 정보 불러오기 실패");

                        if (task.Exception != null)
                            Debug.LogError(task.Exception);
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (!snapshot.Exists)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("유저 데이터가 없습니다.");
                    });
                    return;
                }

                currentCoin = GetInt(snapshot.Child("Coin").Value, 0);

                if (snapshot.Child("Inventory").Value != null)
                {
                    string inventoryJson = snapshot.Child("Inventory").Value.ToString();
                    inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);
                }
                else
                {
                    inventory = CreateEmptyInventory();
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshMyUI();
                    SetMessage("내 정보 불러오기 완료");
                });
            });
    }

    public void LoadAuctionItems()
    {
        reference
            .Child("AuctionItems")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("경매장 목록 불러오기 실패");

                        if (task.Exception != null)
                            Debug.LogError(task.Exception);
                    });
                    return;
                }

                List<AuctionItemData> itemList = new List<AuctionItemData>();

                DataSnapshot snapshot = task.Result;

                if (snapshot.Exists)
                {
                    foreach (DataSnapshot child in snapshot.Children)
                    {
                        AuctionItemData data = MakeAuctionData(child);

                        if (data != null)
                            itemList.Add(data);
                    }
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshAuctionList(itemList);
                    SetMessage("경매장 목록 불러오기 완료");
                });
            });
    }

    public void OnClickRegisterSell()
    {
        if (string.IsNullOrEmpty(userKey))
        {
            SetMessage("로그인 정보가 없습니다.");
            return;
        }

        string itemName = GetSelectedItemName();

        if (!int.TryParse(CountInput.text, out int sellCount) || sellCount <= 0)
        {
            SetMessage("판매 수량을 올바르게 입력하세요.");
            return;
        }

        if (!int.TryParse(PriceInput.text, out int price) || price <= 0)
        {
            SetMessage("판매 가격을 올바르게 입력하세요.");
            return;
        }

        RegisterSellItem(itemName, sellCount, price);
    }

    void RegisterSellItem(string itemName, int sellCount, int price)
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("판매 등록 실패 : 유저 정보 불러오기 실패");

                        if (task.Exception != null)
                            Debug.LogError(task.Exception);
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (!snapshot.Exists)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("판매 등록 실패 : 유저 데이터 없음");
                    });
                    return;
                }

                Dictionary<string, int> freshInventory;

                if (snapshot.Child("Inventory").Value != null)
                {
                    string inventoryJson = snapshot.Child("Inventory").Value.ToString();
                    freshInventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);
                }
                else
                {
                    freshInventory = CreateEmptyInventory();
                }

                if (!freshInventory.ContainsKey(itemName))
                    freshInventory[itemName] = 0;

                if (freshInventory[itemName] < sellCount)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("판매 등록 실패 : 아이템 수량 부족");
                    });
                    return;
                }

                // 판매 등록 순간 판매자 인벤토리에서 아이템 차감
                freshInventory[itemName] -= sellCount;

                string newInventoryJson = JsonConvert.SerializeObject(freshInventory);

                DatabaseReference auctionRef = reference.Child("AuctionItems").Push();
                string auctionKey = auctionRef.Key;

                Dictionary<string, object> updateData = new Dictionary<string, object>();

                updateData["UserInfo/" + userKey + "/Inventory"] = newInventoryJson;

                updateData["AuctionItems/" + auctionKey + "/ItemName"] = itemName;
                updateData["AuctionItems/" + auctionKey + "/Count"] = sellCount;
                updateData["AuctionItems/" + auctionKey + "/Price"] = price;

                updateData["AuctionItems/" + auctionKey + "/SellerKey"] = userKey;
                updateData["AuctionItems/" + auctionKey + "/SellerNickName"] = nickName;

                updateData["AuctionItems/" + auctionKey + "/BuyerKey"] = "";
                updateData["AuctionItems/" + auctionKey + "/BuyerNickName"] = "";

                updateData["AuctionItems/" + auctionKey + "/Status"] = "Selling";
                updateData["AuctionItems/" + auctionKey + "/CreatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                updateData["AuctionItems/" + auctionKey + "/SoldAt"] = "";

                reference.UpdateChildrenAsync(updateData).ContinueWith(saveTask =>
                {
                    if (saveTask.IsFaulted || saveTask.IsCanceled)
                    {
                        dispatcher.Enqueue(() =>
                        {
                            SetMessage("판매 등록 저장 실패");

                            if (saveTask.Exception != null)
                                Debug.LogError(saveTask.Exception);
                        });
                        return;
                    }

                    dispatcher.Enqueue(() =>
                    {
                        inventory = freshInventory;
                        RefreshMyUI();

                        CountInput.text = "";
                        PriceInput.text = "";

                        SetMessage(itemName + " 판매 등록 완료");

                        LoadAuctionItems();
                    });
                });
            });
    }

    public void BuyAuctionItem(AuctionItemData auctionData)
    {
        if (auctionData == null)
            return;

        if (auctionData.SellerKey == userKey)
        {
            SetMessage("자신이 등록한 아이템은 구매할 수 없습니다.");
            return;
        }

        reference
            .Child("AuctionItems")
            .Child(auctionData.AuctionKey)
            .GetValueAsync()
            .ContinueWith(itemTask =>
            {
                if (itemTask.IsFaulted || itemTask.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("구매 실패 : 판매글 확인 실패");

                        if (itemTask.Exception != null)
                            Debug.LogError(itemTask.Exception);
                    });
                    return;
                }

                DataSnapshot itemSnapshot = itemTask.Result;

                if (!itemSnapshot.Exists)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("구매 실패 : 판매글이 없습니다.");
                    });
                    return;
                }

                AuctionItemData latestAuctionData = MakeAuctionData(itemSnapshot);

                if (latestAuctionData.Status != "Selling")
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("이미 판매 완료된 아이템입니다.");
                        LoadAuctionItems();
                    });
                    return;
                }

                LoadBuyerAndSellerThenBuy(latestAuctionData);
            });
    }

    void LoadBuyerAndSellerThenBuy(AuctionItemData auctionData)
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .GetValueAsync()
            .ContinueWith(buyerTask =>
            {
                if (buyerTask.IsFaulted || buyerTask.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("구매 실패 : 구매자 정보 불러오기 실패");

                        if (buyerTask.Exception != null)
                            Debug.LogError(buyerTask.Exception);
                    });
                    return;
                }

                DataSnapshot buyerSnapshot = buyerTask.Result;

                if (!buyerSnapshot.Exists)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("구매 실패 : 구매자 데이터 없음");
                    });
                    return;
                }

                reference
                    .Child("UserInfo")
                    .Child(auctionData.SellerKey)
                    .GetValueAsync()
                    .ContinueWith(sellerTask =>
                    {
                        if (sellerTask.IsFaulted || sellerTask.IsCanceled)
                        {
                            dispatcher.Enqueue(() =>
                            {
                                SetMessage("구매 실패 : 판매자 정보 불러오기 실패");

                                if (sellerTask.Exception != null)
                                    Debug.LogError(sellerTask.Exception);
                            });
                            return;
                        }

                        DataSnapshot sellerSnapshot = sellerTask.Result;

                        if (!sellerSnapshot.Exists)
                        {
                            dispatcher.Enqueue(() =>
                            {
                                SetMessage("구매 실패 : 판매자 데이터 없음");
                            });
                            return;
                        }

                        ProcessBuy(auctionData, buyerSnapshot, sellerSnapshot);
                    });
            });
    }

    void ProcessBuy(AuctionItemData auctionData, DataSnapshot buyerSnapshot, DataSnapshot sellerSnapshot)
    {
        int buyerCoin = GetInt(buyerSnapshot.Child("Coin").Value, 0);
        int sellerCoin = GetInt(sellerSnapshot.Child("Coin").Value, 0);

        if (buyerCoin < auctionData.Price)
        {
            dispatcher.Enqueue(() =>
            {
                SetMessage("코인이 부족합니다.");
            });
            return;
        }

        Dictionary<string, int> buyerInventory;

        if (buyerSnapshot.Child("Inventory").Value != null)
        {
            string buyerInventoryJson = buyerSnapshot.Child("Inventory").Value.ToString();
            buyerInventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(buyerInventoryJson);
        }
        else
        {
            buyerInventory = CreateEmptyInventory();
        }

        if (!buyerInventory.ContainsKey(auctionData.ItemName))
            buyerInventory[auctionData.ItemName] = 0;

        buyerInventory[auctionData.ItemName] += auctionData.Count;

        int newBuyerCoin = buyerCoin - auctionData.Price;
        int newSellerCoin = sellerCoin + auctionData.Price;

        string newBuyerInventoryJson = JsonConvert.SerializeObject(buyerInventory);

        Dictionary<string, object> updateData = new Dictionary<string, object>();

        // 구매자 코인 감소
        updateData["UserInfo/" + userKey + "/Coin"] = newBuyerCoin;

        // 구매자 인벤토리 증가
        updateData["UserInfo/" + userKey + "/Inventory"] = newBuyerInventoryJson;

        // 판매자 코인 증가
        updateData["UserInfo/" + auctionData.SellerKey + "/Coin"] = newSellerCoin;

        // 판매 완료 처리
        updateData["AuctionItems/" + auctionData.AuctionKey + "/Status"] = "Sold";
        updateData["AuctionItems/" + auctionData.AuctionKey + "/BuyerKey"] = userKey;
        updateData["AuctionItems/" + auctionData.AuctionKey + "/BuyerNickName"] = nickName;
        updateData["AuctionItems/" + auctionData.AuctionKey + "/SoldAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        reference.UpdateChildrenAsync(updateData).ContinueWith(saveTask =>
        {
            if (saveTask.IsFaulted || saveTask.IsCanceled)
            {
                dispatcher.Enqueue(() =>
                {
                    SetMessage("구매 저장 실패");

                    if (saveTask.Exception != null)
                        Debug.LogError(saveTask.Exception);
                });
                return;
            }

            dispatcher.Enqueue(() =>
            {
                currentCoin = newBuyerCoin;
                inventory = buyerInventory;

                RefreshMyUI();
                LoadAuctionItems();

                SetMessage(
                    "구매 완료\n" +
                    "아이템 : " + auctionData.ItemName + "\n" +
                    "수량 : " + auctionData.Count + "\n" +
                    "사용 코인 : " + auctionData.Price
                );
            });
        });
    }

    void RefreshAuctionList(List<AuctionItemData> itemList)
    {
        if (Content == null || AuctionItemPrefab == null)
            return;

        for (int i = Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Content.GetChild(i).gameObject);
        }

        foreach (AuctionItemData data in itemList)
        {
            AuctionItemUI itemUI = Instantiate(AuctionItemPrefab, Content);
            itemUI.SetData(data, this, userKey);
        }
    }

    void RefreshMyUI()
    {
        if (CoinText != null)
            CoinText.text = "Coin : " + currentCoin;

        if (InventoryText != null)
        {
            InventoryText.text =
                "내 인벤토리\n" +
                "Antidote : " + GetItemCount("Antidote") + "\n" +
                "Degassing : " + GetItemCount("Degassing") + "\n" +
                "Spanner : " + GetItemCount("Spanner") + "\n" +
                "ShotGun : " + GetItemCount("ShotGun");
        }
    }

    string GetSelectedItemName()
    {
        if (ItemDropdown == null)
            return "Antidote";

        int index = ItemDropdown.value;

        if (index < 0 || index >= itemNames.Length)
            return "Antidote";

        return itemNames[index];
    }

    int GetItemCount(string itemName)
    {
        if (inventory == null)
            return 0;

        if (!inventory.ContainsKey(itemName))
            return 0;

        return inventory[itemName];
    }

    Dictionary<string, int> CreateEmptyInventory()
    {
        Dictionary<string, int> newInventory = new Dictionary<string, int>();

        newInventory["Antidote"] = 0;
        newInventory["Degassing"] = 0;
        newInventory["Spanner"] = 0;
        newInventory["ShotGun"] = 0;

        return newInventory;
    }

    AuctionItemData MakeAuctionData(DataSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.Exists)
            return null;

        AuctionItemData data = new AuctionItemData();

        data.AuctionKey = snapshot.Key;

        data.ItemName = GetString(snapshot.Child("ItemName").Value, "");
        data.Count = GetInt(snapshot.Child("Count").Value, 0);
        data.Price = GetInt(snapshot.Child("Price").Value, 0);

        data.SellerKey = GetString(snapshot.Child("SellerKey").Value, "");
        data.SellerNickName = GetString(snapshot.Child("SellerNickName").Value, "");

        data.BuyerKey = GetString(snapshot.Child("BuyerKey").Value, "");
        data.BuyerNickName = GetString(snapshot.Child("BuyerNickName").Value, "");

        data.Status = GetString(snapshot.Child("Status").Value, "Selling");

        data.CreatedAt = GetString(snapshot.Child("CreatedAt").Value, "");
        data.SoldAt = GetString(snapshot.Child("SoldAt").Value, "");

        return data;
    }

    string GetString(object value, string defaultValue)
    {
        if (value == null)
            return defaultValue;

        return value.ToString();
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

        Debug.Log(message);
    }

    public void MoveInventoryScene()
    {
        SceneManager.LoadScene(InventorySceneName);
    }
}