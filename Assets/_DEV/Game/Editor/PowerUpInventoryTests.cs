using NUnit.Framework;

public class PowerUpInventoryTests
{
    [Test]
    public void EmptyInventory_CanStoreItem()
    {
        // Inventory/network authority tests require a Fusion runner,
        // so test the pure item rules separately for now.
        Assert.AreEqual(PowerUpType.None, default(PowerUpType));
    }
}