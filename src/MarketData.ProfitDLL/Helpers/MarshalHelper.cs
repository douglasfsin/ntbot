using System.Runtime.InteropServices;

namespace ProfitDLLClient.Helpers;

/// <summary>
/// Helper para operações de marshaling de dados não gerenciados
/// </summary>
public static class MarshalHelper
{
    /// <summary>
    /// Converte buffer de ofertas não gerenciado para lista gerenciada
    /// </summary>
    public static void MarshalOfferBuffer(IntPtr buffer, List<TConnectorOffer> lstOffer)
    {
        lstOffer.Clear();
        var offset = 0;

        // Lê o cabeçalho
        var qtdOffer = Marshal.ReadInt32(buffer, offset);
        offset += 4;

        // Lê as ofertas
        for (int i = 0; i < qtdOffer; i++)
        {
            var bufferOffer = new byte[53];
            Marshal.Copy(buffer + offset, bufferOffer, 0, 53);

            var price = BitConverter.ToDouble(bufferOffer, 0);
            var qtd = BitConverter.ToInt64(bufferOffer, 8);
            var agent = BitConverter.ToInt32(bufferOffer, 16);
            var offerID = BitConverter.ToInt64(bufferOffer, 20);

            var strDate = bufferOffer[30..].Select(x => (char)x);
            var date = DateTime.ParseExact(strDate.ToArray(), "dd/MM/yyyy HH:mm:ss.fff", null);

            var offer = new TConnectorOffer(price, qtd, agent, offerID, date);
            lstOffer.Add(offer);

            offset += 53;
        }

        // Lê o rodapé
        var pointerSize = IntPtr.Size;
        var trailer = new byte[pointerSize - offset];
        Marshal.Copy(buffer + offset, trailer, 0, trailer.Length);

        var flags = (OfferBookFlags)BitConverter.ToUInt32(trailer);
    }

    /// <summary>
    /// Lê identificador de conta do usuário
    /// </summary>
    public static TConnectorAccountIdentifier ReadAccountId()
    {
        var accountId = new TConnectorAccountIdentifier { Version = 0 };

        Console.Write("broker: ");
        var strBroker = Console.ReadLine();
        if (int.TryParse(strBroker, out int broker))
        {
            accountId.BrokerID = broker;
        }

        Console.Write("id: ");
        accountId.AccountID = Console.ReadLine() ?? "";

        Console.Write("subid: ");
        accountId.SubAccountID = Console.ReadLine() ?? "";

        return accountId;
    }

    /// <summary>
    /// Lê identificador de ativo do usuário
    /// </summary>
    public static TConnectorAssetIdentifier ReadAssetId()
    {
        var assetId = new TConnectorAssetIdentifier { Version = 0 };

        Console.Write("ticker: ");
        assetId.Ticker = Console.ReadLine() ?? "";

        Console.Write("exchange: ");
        assetId.Exchange = Console.ReadLine() ?? "";

        assetId.FeedType = 0;

        return assetId;
    }
}
