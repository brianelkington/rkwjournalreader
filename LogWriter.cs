using System.IO;
using System.Text;
/// <summary>
/// A TextWriter that duplicates writes to console and log.
/// </summary>
public class LogWriter : TextWriter
{
  private readonly TextWriter _primary, _secondary;
  public LogWriter(TextWriter primary, TextWriter secondary)
  {
    _primary = primary;
    _secondary = secondary;
  }
  public override Encoding Encoding => _primary.Encoding;
  public override void Write(char value)
  {
    _primary.Write(value);
    _secondary.Write(value);
  }
  public override void Write(string value)
  {
    _primary.Write(value);
    _secondary.Write(value);
  }
  public override void WriteLine(string value)
  {
    _primary.WriteLine(value);
    _secondary.WriteLine(value);
  }
  public override void Flush()
  {
    _primary.Flush();
    _secondary.Flush();
  }
}
