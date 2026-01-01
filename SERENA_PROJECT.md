# Projet Serena - LiveMap 3D pour Vintage Story

> Mémoire de projet pour la contribution d'une vue 3D à LiveMap Revival

---

## Contexte

**Date de début :** 2026-01-01
**Contributeur :** Fieve
**Repo cible :** https://github.com/mja00/VS-LiveMap-Revival
**Objectif :** Ajouter une option de carte 3D au mod LiveMap Revival

---

## Compréhension du projet LiveMap Revival

### Architecture actuelle

```
┌─────────────────────────────────────────────────────────────┐
│            Serveur Vintage Story                            │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  LiveMap Mod (.NET 8.0)                                │ │
│  │  ├─ LiveMapMod.cs (Point d'entrée ModSystem)           │ │
│  │  ├─ LiveMap.cs (Orchestrateur serveur)                 │ │
│  │  ├─ WebServer (HttpListener, port 8080)                │ │
│  │  ├─ RenderTaskManager (génération des tuiles async)    │ │
│  │  ├─ RendererRegistry (Basic, Sepia)                    │ │
│  │  └─ LayerRegistry (Players, Spawn, Traders...)         │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              ↓ HTTP :8080
┌─────────────────────────────────────────────────────────────┐
│  Frontend Web (TypeScript + Leaflet.js)                     │
│  ├─ LiveMap.ts (classe principale)                          │
│  ├─ LiveTileLayer.ts (tuiles de carte)                      │
│  ├─ PlayersLayer.ts (marqueurs joueurs temps réel)          │
│  └─ Controls (zoom, layers, sidebar...)                     │
└─────────────────────────────────────────────────────────────┘
```

### Fichiers clés à connaître

| Fichier | Rôle |
|---------|------|
| `src/LiveMapMod.cs` | Point d'entrée du mod (ModSystem) |
| `src/LiveMap.cs` | Orchestrateur principal côté serveur |
| `src/render/Renderer.cs` | Classe abstraite pour les renderers |
| `src/render/BasicRenderer.cs` | Renderer par défaut (couleurs naturelles) |
| `src/task/RenderTaskManager.cs` | Gestion du rendu asynchrone |
| `src/data/Colormap.cs` | Mapping Block ID → couleurs RGBA |
| `web/src/LiveMap.ts` | Classe principale frontend (Leaflet) |
| `web/src/layer/LiveTileLayer.ts` | Gestion des tuiles |

### Stack technique

**Backend :**
- .NET 8.0
- Vintage Story API
- Newtonsoft.Json
- HttpListener (serveur web intégré)

**Frontend :**
- TypeScript 5.4
- Leaflet.js 1.9.4
- Webpack 5
- SASS

---

## Recherches effectuées

### Mods de carte 3D existants

#### map3d (Vintage Story)
- **Auteur :** Zokora
- **Version :** 0.1.0
- **Lien :** https://mods.vintagestory.at/map3d
- **Concept :** Affiche le terrain 3D sur des objets in-game (tables, projecteurs)
- **Note :** Performance limitée au-delà de 1000x1000

#### BlueMap (Minecraft) - RÉFÉRENCE PRINCIPALE
- **Lien :** https://github.com/BlueMap-Minecraft/BlueMap
- **Stack :** Java + Three.js + WebGL + Vue.js
- **Architecture :** Lecture chunks → Modèles 3D → Three.js → Navigateur
- **Port :** 8100
- **Points forts :** Rendu async, bien documenté, modulaire

### Technologies 3D pour le web

| Technologie | Description | Pertinence |
|-------------|-------------|------------|
| **Three.js** | Bibliothèque 3D WebGL | ⭐⭐⭐ Idéal pour voxels |
| **Babylon.js** | Moteur 3D orienté jeux | ⭐⭐ Alternative solide |
| **three-geo** | Visualisation géographique 3D | ⭐ Spécialisé terrain |

### Ressources d'apprentissage

- [Three.js Voxel Geometry](https://threejsfundamentals.org/threejs/lessons/threejs-voxel-geometry.html)
- [Voxel Art with Three.js](https://tympanus.net/codrops/2023/03/28/turning-3d-models-to-voxel-art-with-three-js/)
- [BlueMap Source Code](https://github.com/BlueMap-Minecraft/BlueMap)

---

## Plan d'implémentation

### Phase 1 : Préparation

- [ ] Fork le repo mja00/VS-LiveMap-Revival
- [ ] Configurer l'environnement de dev
- [ ] Étudier le code de BlueMap (Three.js usage)
- [ ] Ouvrir une Issue pour discuter avec les mainteneurs

### Phase 2 : Backend (C#)

- [ ] Créer `src/render/Renderer3D.cs`
- [ ] Exporter les heightmaps (données d'altitude)
- [ ] Créer un format de données optimisé pour les chunks 3D
- [ ] Ajouter endpoint `/data/chunks/{x}/{z}.json`
- [ ] Ajouter configuration `Config.Enable3D`

### Phase 3 : Frontend (TypeScript)

- [ ] Ajouter `three` dans `web/package.json`
- [ ] Créer `web/src/ThreeJSMap.ts` (alternative à Leaflet)
- [ ] Créer `web/src/VoxelRenderer.ts`
- [ ] Créer `web/src/CameraController.ts`
- [ ] Ajouter toggle 2D/3D dans l'interface
- [ ] Intégrer avec les layers existants (players, markers)

### Phase 4 : Optimisation

- [ ] Implémenter Level of Detail (LOD)
- [ ] Frustum culling (ne rendre que le visible)
- [ ] Chargement progressif des chunks
- [ ] Compression des données

### Phase 5 : Finalisation

- [ ] Tests de performance
- [ ] Documentation
- [ ] Soumettre la Pull Request

---

## Architecture proposée pour le mode 3D

```
┌─────────────────────────────────────────────────────────────┐
│  Backend - Nouvelles composantes                            │
├─────────────────────────────────────────────────────────────┤
│  src/render/Renderer3D.cs                                   │
│  ├─ Génère les données de hauteur par chunk                 │
│  ├─ Exporte les types de blocs visibles                     │
│  └─ Format: { heightmap: [], blocks: [], colors: [] }       │
│                                                             │
│  src/data/ChunkData3D.cs                                    │
│  ├─ Structure optimisée pour le transfert                   │
│  └─ Compression RLE pour les données répétitives            │
│                                                             │
│  Endpoints:                                                 │
│  ├─ GET /data/3d/chunks/{x}/{z}.json                        │
│  └─ GET /data/3d/settings.json                              │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  Frontend - Module 3D                                       │
├─────────────────────────────────────────────────────────────┤
│  web/src/3d/                                                │
│  ├─ ThreeJSMap.ts        # Classe principale Three.js       │
│  ├─ VoxelGeometry.ts     # Génération géométrie voxels      │
│  ├─ ChunkMesh.ts         # Mesh par chunk                   │
│  ├─ CameraController.ts  # Navigation orbitale/FPS          │
│  ├─ ChunkLoader.ts       # Chargement async des chunks      │
│  └─ LODManager.ts        # Gestion niveau de détail         │
│                                                             │
│  web/src/control/                                           │
│  └─ ViewModeControl.ts   # Toggle 2D/3D                     │
└─────────────────────────────────────────────────────────────┘
```

### Format de données chunk 3D (proposition)

```json
{
  "x": 0,
  "z": 0,
  "heightmap": [64, 65, 63, ...],
  "blocks": [
    {"y": 64, "type": "grass", "color": "#4a7c23"},
    {"y": 63, "type": "dirt", "color": "#8b6914"}
  ],
  "compressed": true,
  "version": 1
}
```

---

## Défis identifiés

| Défi | Impact | Solution envisagée |
|------|--------|-------------------|
| Performance rendu | Élevé | LOD + frustum culling |
| Taille des données | Moyen | Compression RLE, streaming |
| Synchronisation 2D/3D | Moyen | État partagé, events |
| Compatibilité navigateurs | Faible | WebGL2 fallback WebGL1 |
| Mémoire client | Élevé | Unload chunks distants |

---

## Contacts et ressources

### Mainteneurs du projet
- **mja00** - Fork actuel, mainteneur principal
- **BillyGalbreath** - Auteur original

### Communauté
- Issues GitHub : https://github.com/mja00/VS-LiveMap-Revival/issues
- ModDB : https://mods.vintagestory.at/livemaprevival

### Références techniques
- BlueMap : https://github.com/BlueMap-Minecraft/BlueMap
- Three.js : https://threejs.org/docs/
- map3d (VS) : https://mods.vintagestory.at/map3d

---

## Notes et idées

### Idées futures
- Mode VR avec WebXR
- Export du monde en format 3D (GLTF/OBJ)
- Mesure de distances en 3D
- Annotations 3D (markers avec altitude)
- Timeline pour voir l'évolution du monde

### Questions ouvertes
- Faut-il garder Leaflet pour le mode 2D ou tout migrer vers Three.js ?
- Quel niveau de détail minimum pour les performances mobiles ?
- Comment gérer les caves/souterrains ?

---

## Journal de progression

### 2026-01-01
- [x] Analyse complète du codebase LiveMap Revival
- [x] Recherche des solutions 3D existantes (BlueMap, map3d, Three.js)
- [x] Documentation de l'architecture complète du mod
- [x] Création du plan de projet Serena
- [x] Identification des fichiers clés à modifier
- [ ] Prochaine étape : Fork et setup environnement

---

## État de la session (sauvegarde)

### Ce qui a été accompli cette session :

1. **Exploration complète du codebase** :
   - Point d'entrée : `src/LiveMapMod.cs`
   - Orchestrateur : `src/LiveMap.cs`
   - Configuration : `src/configuration/Config.cs`
   - Serveur web intégré sur port 8080

2. **Recherches web effectuées** :
   - BlueMap (Minecraft) = référence pour carte 3D avec Three.js
   - map3d (Vintage Story) = mod 3D existant par Zokora
   - Technologies : Three.js recommandé pour le rendu voxel

3. **Plan établi** :
   - 5 phases d'implémentation définies
   - Architecture backend/frontend proposée
   - Format de données chunk 3D spécifié

### Pour reprendre :

```
Dis-moi "continue le projet Serena" et je reprendrai là où on s'est arrêté !
```

### Plugins installés (à vérifier après restart) :
- github
- supabase
- commit-commands
- security-guidance
- sentry
- csharp-lsp
- clangd-lsp
- greptile

---

*Ce document sera mis à jour au fur et à mesure de l'avancement du projet.*
