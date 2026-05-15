# Projet final - Environnements immersifs (Hiver 2026)

Jeu video en realite virtuelle (Oculus Quest) realise pour le cours **420-6B3-VI**.

## Equipe

- **Morgan Boissonneault** - DA: **2353421**
- **Justin Chaput** - DA: **2238424**

## Presentation du jeu

Le projet est un jeu VR de type **action/survival par vagues**: le joueur doit eliminer les cibles ennemies au marteau avant la fin du temps.

- **Objectif principal:** survivre et completer le plus de manches possible
- **Defaite (Game Over):** temps ecoule ou ennemi qui touche le joueur
- **Victoire:** toutes les cibles de toutes les manches eliminees
- **Recommencement:** retour au menu pour relancer une partie rapidement

## Plateforme et technologies

- **Casque cible:** Meta/Oculus Quest
- **Moteur:** Unity 6 (Editor `6000.3.8f1`)
- **XR:** OpenXR + XR Interaction Toolkit (`com.unity.xr.interaction.toolkit` `3.3.1`)
- **Langage:** C#

## Demarrage du projet

### Option 1 - Executer l'APK

1. Connecter le casque Quest en mode developpeur.
2. Installer `apk.apk` (ADB ou SideQuest).
3. Lancer l'application depuis le casque.

### Option 2 - Ouvrir dans Unity

1. Ouvrir le dossier du projet avec Unity Hub.
2. Utiliser Unity **6000.3.8f1**.
3. Ouvrir la scene: `Assets/Scenes/Principal.unity`.
4. Faire un **Build & Run Android** pour le casque Quest.

## Fonctionnalites principales

- Menu de depart avec bouton **Jouer**
- Interface en jeu (temps restant, manche, nombre de cibles, record)
- Ennemis/cibles avec collisions et systeme de destruction
- Sons d'impact, d'apparition et d'ambiance d'action
- Ecrans de fin de partie (victoire/defaite) avec reprise facile

## Structure du projet

- `Assets/Scenes/Principal.unity` : scene principale du jeu
- `Assets/Scripts/GameManager.cs` : logique de manches, UI et conditions de victoire/defaite
- `Assets/Scripts/MarteauThor.cs` : interactions du marteau et detection des coups
- `Assets/Scripts/EnemyHammerTarget.cs` : comportement des ennemis
- `Assets/Scripts/Cible.cs` et `CibleRonde.cs` : cibles frappables
- `Assets/Scripts/Chrono.cs` : minuterie de manche

## Correspondance avec les criteres de l'enonce

| Critere                | Implementation dans le projet                                     |
| ---------------------- | ----------------------------------------------------------------- |
| Scene(s) adequates     | Scene VR thematique avec gameplay centre sur les vagues d'ennemis |
| Objectif clair         | Survie et progression par manches avec indicateurs UI             |
| Bonnes pratiques VR    | Interactions XR standard, lisibilite UI, gameplay axe confort     |
| Menus                  | Menu de depart + interfaces de fin de partie                      |
| Sons/musique           | Sons lies aux actions (impact, apparition, etc.)                  |
| Interactions           | Frappe physique au marteau, collisions et feedback                |
| Recommencement         | Retour menu + redemarrage rapide                                  |
| Logique/structure code | Scripts separes par responsabilite (manager, cible, arme, chrono) |

---

Depot public - Aide Chat GPT
