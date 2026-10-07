namespace NetControl.Windows;
public sealed record RouterDevice(string Id,string Name,string Host,int Port,string Username,string Password,bool Remote=false);
public sealed record ApiResult(Dictionary<string,string> Values);
