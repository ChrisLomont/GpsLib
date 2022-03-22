namespace Lomont.Gps
{
    /// <summary>
    /// Does any entry have a NaN floating point value?
    /// </summary>
    public interface IHasNaN
    {
        bool HasNaN => false;
    }
}
