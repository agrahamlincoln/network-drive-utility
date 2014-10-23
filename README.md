network-drive-utility
=====================

Provides information about mapped network drives across a network. 
This program has the following features:

- Enumerate a list of Network Drives utilized in your network
- Unmap unwanted fileshares using a blacklist (with wildcard support)
- Generate statistics about fileshares used in your network.
- SQLite Support
- Small footprint

Flags (Run these arguments when executing the program)
- logging - Enables advanced logging to the %appdata% folder

Full Program log - Detailed information when new fileshares are added and removed
  - Fileshare Unmaps
  - Fileshare Additions

Upcoming/Planned features

- Fileshare Mapping
- Statistics: count of each fileshare's occurrence

Background

This program was created with the intention of auditing our network's fileshares. I wanted to establish a list of all fileshares that our users access. Because our environment holds two unique domains that are not trusted, and are not in the same forest, this application has been created with the intention of helping us map those fileshares that are in the secondary, untrusted domain. The second main function of the application is to remove strange fileshares that no longer exist, but are still mapped on users' pc's. This occurs if we rename a server, remove a share from a server, or perhaps remove access for that user. Windows group policy has some support for this, but there have been strange occurrences on our network where our rules were removing all manually-added fileshares (like those in the secondary domain). Now our group policy handles all of our domain's fileshares, and we intend to use either windows logon scripts, or this application to map the secondary domain's fileshares.

I attempted to make this program fairly modular and scalable. There is fairly advanced logging built in, and the app.config file can be modified to suit any network's storage locations.

Credits to any code I've found/used/modified are found in the comments.

Deployment

I recommend you deploy this application using scheduled tasks upon login. If you set this up via group policy, you may easily set arguments and when to run the application (I recommend 30seconds after log in of any user), and can easily remove it by changing the group policy object to remove the scheduled task. This also has built in handling if the executible is missing, the scheduled task will fail and the user will not notice anything.
