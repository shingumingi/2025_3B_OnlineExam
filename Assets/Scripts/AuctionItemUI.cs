using UnityEngine;
using UnityEngine.UI;

public class AuctionItemUI : MonoBehaviour
{
    [SerializeField] Text ItemNameText;
    [SerializeField] Text SellerText;
    [SerializeField] Text CountText;
    [SerializeField] Text PriceText;
    [SerializeField] Text StatusText;
    [SerializeField] Button BuyButton;

    AuctionItemData data;
    AuctionManager manager;

    public void SetData(AuctionItemData auctionData, AuctionManager auctionManager, string currentUserKey)
    {
        data = auctionData;
        manager = auctionManager;

        if (ItemNameText != null)
            ItemNameText.text = data.ItemName;

        if (SellerText != null)
            SellerText.text = "판매자 : " + data.SellerNickName;

        if (CountText != null)
            CountText.text = "수량 : " + data.Count;

        if (PriceText != null)
            PriceText.text = "가격 : " + data.Price;

        if (StatusText != null)
            StatusText.text = "상태 : " + data.Status;

        bool canBuy =
            data.Status == "Selling" &&
            data.SellerKey != currentUserKey;

        if (BuyButton != null)
            BuyButton.interactable = canBuy;
    }

    public void OnClickBuy()
    {
        if (manager == null || data == null)
            return;

        manager.BuyAuctionItem(data);
    }
}