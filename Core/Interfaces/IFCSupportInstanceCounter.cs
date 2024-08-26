namespace SIPS.Framework.Core.Interfaces
{
    public interface IFCResponse
    {
        string StatusMessage { get; set; } 
        string ErrorMessage { get; set; }
        bool Success { get; set; }
        object Value { get; set; }
    }
    public interface IFCSupportInstanceCounter
    {
        int InstanceId { get; }
    }
}
