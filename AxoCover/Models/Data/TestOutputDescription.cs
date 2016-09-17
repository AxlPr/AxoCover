﻿namespace AxoCover.Models.Data
{
  public class TestOutputDescription
  {
    public string[] Directories { get; private set; }
    public string[] Files { get; private set; }
    public double Size { get; private set; }
    public TestOutputDescription(string[] directories, string[] files, double size)
    {
      Directories = directories;
      Files = files;
      Size = size;
    }
  }
}
