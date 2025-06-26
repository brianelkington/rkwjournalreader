using System.IO;
using System.Text;

namespace read_journal
{
  // Helper to duplicate Console output to file
  class LogWriter : TextWriter
  {
    private readonly TextWriter _out1;
    private readonly TextWriter _out2;

    public LogWriter(TextWriter out1, TextWriter out2)
    {
      _out1 = out1;
      _out2 = out2;
    }

    public override Encoding Encoding => _out1.Encoding;

    public override void Write(char value)
    {
      _out1.Write(value);
      _out2.Write(value);
    }

    public override void Write(string value)
    {
      _out1.Write(value);
      _out2.Write(value);
    }

    public override void WriteLine(string value)
    {
      _out1.WriteLine(value);
      _out2.WriteLine(value);
    }
  }
}