﻿// COPYRIGHT 2015 by the Open Rails project.
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


namespace Orts.Common.Threading
{
    public struct CancellationToken
    {
        private readonly CancellationTokenSource source;

        public CancellationToken(CancellationTokenSource source)
        {
            this.source = source;
        }

        public bool IsCancellationRequested
        {
            get
            {
                source.DoPing();
                return source.IsCancellationRequested;
            }
        }
    }
}
