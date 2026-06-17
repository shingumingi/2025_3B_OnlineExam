[System.Serializable]
public class AuctionItemData
{
    public string AuctionKey;

    public string ItemName;
    public int Count;
    public int Price;

    public string SellerKey;
    public string SellerNickName;

    public string BuyerKey;
    public string BuyerNickName;

    public string Status;

    public string CreatedAt;
    public string SoldAt;
}