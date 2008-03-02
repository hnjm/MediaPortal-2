#region Copyright (C) 2008 Team MediaPortal

/*
    Copyright (C) 2008 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion
using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Core.Settings;

namespace MediaPortal.Plugins.ExtensionUpdater
{
  [Serializable]
  class ExtensionUpdaterSettings
  {
    private int _myTaskID;
    private int _updaterTaskID;
    private string _updateUrl = "";

    [Setting(SettingScope.Global, 0)]
    public int TaskId
    {
      get
      {
        return _myTaskID;
      }
      set
      {
        _myTaskID = value;
      }
    }
    
    [Setting(SettingScope.Global, 0)]
    public int UpdaterTaskId
    {
      get
      {
        return _updaterTaskID;
      }
      set
      {
        _updaterTaskID = value;
      }
    }

    /// <summary>
    /// Gets or sets the update URL.
    /// </summary>
    /// <value>The update URL.</value>
    /// http://openmaid.team-mediaportal.com/xtern.php?sync
    [Setting(SettingScope.Global, "http://openmaid.team-mediaportal.com/xtern.php?sync")]
    public string UpdateUrl
    {
      get
      {
        return _updateUrl;
      }
      set
      {
        _updateUrl = value;
      }
    }

  }
}
