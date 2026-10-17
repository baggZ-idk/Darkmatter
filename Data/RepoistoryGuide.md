# Repository Guide
Repositories are how Darkmatter finds and installs mods. This guide will help you understand how to make a repository and how to install one.

## Creating a repository
The way repositories work is via raw JSON over HTTPS. The simplest way to host a repository is by using GitHub Raw file URLs. The code block below shows an example repository.

```json
[
  {
    "Name": "Example Mod",
    "Author": "ExampleAuthor",
    "Dependencies": [
      "ExampleLibrary"
    ],
    "install_location": "BepInEx/plugins",
    "git_path": "ExampleMod",
    "Group": "Mod Menu",
    "download_url": "https://github.com/example/ExampleMod/releases/latest/download/ExampleMod.dll"
  },
  {
    "Name": "ExampleLibrary",
    "Author": "ExampleAuthor",
    "Dependencies": [],
    "install_location": "BepInEx/plugins",
    "git_path": "ExampleLibrary",
    "Group": "Libraries",
    "download_url": "https://github.com/example/ExampleLibrary/releases/latest/download/ExampleLibrary.dll"
  }
]
```

Name - The mod name.

Author - The mod author.

Dependencies - Optional, these mods will be installed automatically while installing.

install_location - Where to install the mod.

git_path - Redundant feature, you do not need this (will probably get removed entirely in the future).

Group - What category it goes in, options are "libraries", "Mod Menu", "Cosmetic", "Competitive", "Gameplay", "Quality of Life", and "Mod Checker". Other groups are accepted; they just will not be properly colored.

download_url - A direct download link to the mod.

## Installing a repository
1. Click on the three-line button.
2. Click on Manage Repositories.
3. Set Repository Name to whatever you desire.
4. Paste the Repository Link into the link box.
5. Click the Add button.
6. Click the Save button.
