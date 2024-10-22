namespace meTesting.TransactionIdGenerator;

public class TrGen
{
    public string GetNewId()
    {
        var gu = Guid.NewGuid().ToString("N");
        return $"az.tr.{gu}"; 
    }
}
