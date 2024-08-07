namespace KVPSButter;

[Serializable]
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

[Serializable]
public class InvalidCursorException : Exception
{
    public InvalidCursorException() { }
    public InvalidCursorException(string message) : base(message) { }
    public InvalidCursorException(string message, Exception inner) : base(message, inner) { }
}