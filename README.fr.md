# <img src="AutoStanceXIV/images/icon.png" width="32" alt=""> AutoStanceXIV

🇬🇧 [English version](README.md)

Plugin [Dalamud](https://github.com/goatcorp/Dalamud) pour Final Fantasy XIV qui active ou retire automatiquement la stance de tank, sur tous les tanks :

| Job | Stance |
|---|---|
| Paladin / Gladiateur | Volonté de fer |
| Guerrier / Maraudeur | Défi |
| Chevalier noir | Férocité |
| Pistosabreur | Garde royale |

## Installation

1. En jeu, tape `/xlsettings` et ouvre l'onglet **Expérimental**.
2. Dans **Custom Plugin Repositories**, ajoute cette adresse, clique sur **+**, coche la case puis enregistre :
   ```
   https://raw.githubusercontent.com/Arzhaell/AutoStanceXIV/main/repo.json
   ```
3. Tape `/xlplugins`, cherche **AutoStanceXIV** et installe-le.

## Utilisation

`/autostance` ouvre la configuration :

- **Classique ou avancé** :
  - *Classique* : les mêmes réglages partout.
  - *Avancé* : un onglet de réglages par type d'instance. Les réglages avancés partent d'une copie des réglages classiques.
    - **ALÉA** : tout ce qui est hors instance (ALÉA, monde ouvert, opérations de terrain…)
    - **Donjon** : y compris donjons spéciaux et sans fond, opérations de guilde et chasses aux trésors
    - **Défis** : normaux, extrêmes et irréels
    - **Raid** : raids à 8 — normaux, sadiques et fatals
    - **Alliance** : raids en alliance à 24, y compris chaotiques
- **Mode** : activer automatiquement, retirer automatiquement, ou pause.
- **Quand l'appliquer** :
  - *En permanence* : la stance est remise dans l'état voulu dès qu'elle en sort.
  - *À certains moments* (combinables) : entrée en zone, résurrection, changement de job, compte à rebours (X s avant la fin), au pull (option boss uniquement), reprise de l'instance après un wipe.
- **Options** : seulement en instance, autorisé ou non en combat, affichage dans la barre d'infos serveur, message dans le chat.
- **Langue** : auto (suit Dalamud), anglais ou français.

Changer de mode rapidement :

| Commande | Effet |
|---|---|
| `/autostance on` | garder la stance activée |
| `/autostance off` | garder la stance retirée |
| `/autostance toggle` | basculer entre on et off |
| `/autostance pause` | ne plus toucher à la stance |

Dans la barre d'infos serveur (« Stance: ON ») : clic gauche pour basculer on/off, clic droit pour mettre en pause.

En mode avancé, les commandes et la barre d'infos serveur agissent sur le type d'instance où tu te trouves.

## Avertissement

Comme tout outil tiers, l'utilisation de Dalamud et de ses plugins est contraire aux conditions d'utilisation de FFXIV. Tu l'utilises à tes risques.

## Publier une nouvelle version

Pousser un tag annoté `vX.Y.Z` suffit : le workflow GitHub lance les tests, compile le plugin, crée la release (ses notes sont le message du tag) et met à jour `repo.json`.

```bash
git tag -a v1.2.0 -F notes.txt
git push origin v1.2.0
```
