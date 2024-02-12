using System.Runtime.Serialization;

namespace KVPSButter;

[System.Serializable]
public class InvalidKeyException : Exception
{
    public InvalidKeyException() { }
    public InvalidKeyException(string message) : base(message) { }
    public InvalidKeyException(string message, Exception inner) : base(message, inner) { }
}

[Serializable]
public class InvalidOptionException : Exception
{
    public InvalidOptionException() { }
    public InvalidOptionException(string message) : base(message) { }
    public InvalidOptionException(string message, Exception inner) : base(message, inner) { }
}