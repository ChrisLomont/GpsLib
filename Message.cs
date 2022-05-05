namespace Lomont.Gps
{
    /// <summary>
    /// Base class for gps messages
    /// </summary>
    public class Message
    {

        public string Description { get;  }
        public Message(string description)
        {
            Description = description;
        }

    }
}
