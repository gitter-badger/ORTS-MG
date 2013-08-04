﻿// COPYRIGHT 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using MSTS;

namespace ORTS.Menu
{
    public class Path
    {
        public readonly string Name;
        public readonly string Start;
        public readonly string End;
        public readonly string FilePath;

        internal Path(string filePath)
        {
            if (File.Exists(filePath))
            {
                var showInList = true;
                try
                {
                    var patFile = new PATFile(filePath);
                    showInList = patFile.IsPlayerPath;
                    Name = patFile.Name.Trim();
                    Start = patFile.Start.Trim();
                    End = patFile.End.Trim();
                }
                catch
                {
                    Name = "<load error: " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                }
                if (!showInList) throw new InvalidDataException("Path '" + filePath + "' is excluded.");
                if (string.IsNullOrEmpty(Name)) Name = "<unnamed: " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                if (string.IsNullOrEmpty(Start)) Start = "<unnamed: " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                if (string.IsNullOrEmpty(End)) End = "<unnamed: " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
            else
            {
                Name = Start = End = "<missing: " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
            FilePath = filePath;
        }

        public override string ToString()
        {
            return End;
        }

        public static List<Path> GetPaths(Route route)
        {
            var paths = new List<Path>();
            var directory = System.IO.Path.Combine(route.Path, "PATHS");
            if (Directory.Exists(directory))
            {
                foreach (var path in Directory.GetFiles(directory, "*.pat"))
                {
                    try
                    {
                        paths.Add(new Path(path));
                    }
                    catch { }
                }
            }
            return paths;
        }
    }
}
