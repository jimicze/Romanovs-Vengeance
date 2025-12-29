# Red Alert 2: Romanov's Vengeance

Romanov's Vengeance is a 3rd party [OpenRA](http://www.openra.net) mod based on OpenRA [Red Alert 2](http://www.github.com/OpenRA/ra2) mod. It aims to create a Red Alert 2 with balanced multiplayer experience, improvements that comes from OpenRA and other improvements from more modern Command & Conquer games. A custom campaign is also being planned, but not much of a work is done on it yet.

Please note that mod is still under developement, even the playtest versions are susceptible to bugs. There are still a few features from the original game that are still missing, there are seveal placeholder artwork and new stuff and balancing are always subject to change.

Installing the mod is done the same way as another [OpenRAModSDK](http://www.github.com/OpenRA/OpenRAModSDK) mod.

You can join our Discord server [here](https://discord.gg/SrvArjQ).

You can also follow the developement on [our ModDB Page](https://www.moddb.com/mods/romanovs-vengeance).

## Forking with wiki, issues and pull requests

The README and code are copied automatically when you fork. To keep the rest of the project metadata:

- **Wiki:** clone the original wiki and push it to your fork.
  ```
  git clone https://github.com/MustaphaTR/Romanovs-Vengeance.wiki.git
  cd Romanovs-Vengeance.wiki
  git remote set-url origin https://github.com/<your-username>/Romanovs-Vengeance.wiki.git
  git push --mirror
  ```
- **Issues:** GitHub does not copy issues on fork. With the GitHub CLI (`gh`) and `jq` you can export and recreate them:
  ```
  gh api repos/MustaphaTR/Romanovs-Vengeance/issues --paginate > issues.json
  jq -c '.[] | {title, body, labels: [.labels[].name]}' issues.json | \
    while read issue; do
      gh issue create --repo <your-username>/Romanovs-Vengeance \
        --title "$(echo "$issue" | jq -r .title)" \
        --body "$(echo "$issue" | jq -r .body)" \
        $(echo "$issue" | jq -r '.labels[]? | "--label \(.)"')
    done
  ```
  Authors and timestamps cannot be preserved.
- **Pull requests:** GitHub cannot transfer PRs to a fork. Keep the original repository as an `upstream` remote to reference old PRs, fetch the source branches, or ask contributors to reopen their PRs against your fork.
