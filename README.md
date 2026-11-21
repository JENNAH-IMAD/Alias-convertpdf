# Alias Convert PDF

Alias transforme des relevés et des extraits bancaires en fichiers exploitables dans Excel, Sage 100 et Sage X3. L’application conserve le PDF original, permet de vérifier et de corriger les mouvements lus, puis classe les documents et les résultats par client et compte bancaire.

## Fonctionnalités

| Écran | Ce que vous pouvez faire |
| --- | --- |
| **Vue d’ensemble** | Suivre les imports, les exports et les documents à vérifier. |
| **Conversion & export** | Importer un PDF ou un extrait, choisir la banque et la devise, consulter l’original, corriger les mouvements, puis télécharger le résultat. |
| **Espace clients** | Retrouver les relevés, extraits et écritures archivées. Rechercher un document et filtrer par statut ou modèle d’exportation. |
| **Banques et comptes** | Gérer le référentiel bancaire et renseigner le compte général, le journal et la devise de chaque compte. |
| **Modèles d’exportation** | Configurer et dupliquer les formats CSV et Sage. Modifier les colonnes, l’ordre des champs, les formats de dates et de montants, les séparateurs et l’encodage. |
| **Paramétrage de l’écriture** | Gérer les règles propres aux clients et préparer des pièces équilibrées. Renseigner les comptes de contrepartie par ligne ou par lot. |
| **Historique et accès** | Consulter l’activité selon les permissions et gérer les utilisateurs depuis le compte administrateur. |

### Lecture des PDF

Alias lit d’abord le texte natif. Les pages nécessitant une reconnaissance passent au service local **PaddleOCR**. Le moteur exploite les positions du texte et la structure des tableaux pour aider à reconstituer les mouvements. L’interface signale les anomalies de lecture et permet de contrôler le résultat à côté du PDF. [Documentation du moteur local](services/ocr/README.md).

### Écritures Sage 100 équilibrées

Le journal et le compte général de banque proviennent du compte bancaire sélectionné. Le traitement porte sur l’ensemble des mouvements du relevé ou de l’extrait choisi. Vous pouvez compléter les contreparties, appliquer un taux de TVA aux commissions en lot ou par ligne, puis recalculer les pièces. La TVA apparaît sous la commission et la contrepartie bancaire conserve le total du mouvement.

| Ligne d’une pièce illustrative | Débit (MAD) | Crédit (MAD) |
| --- | ---: | ---: |
| Commission HT | 137,50 | |
| TVA à 20 % | 27,50 | |
| Banque | | 165,00 |

L’aperçu affiche les mouvements à corriger et les totaux débit/crédit. Le téléchargement TXT ou Excel et l’archivage se font depuis le résultat. Un export exceptionnel confirmé avec des anomalies porte la mention **À vérifier**.

## Technologies

| Composant | Technologies |
| --- | --- |
| Interface web | Next.js 16, React 19, TypeScript, Tailwind CSS 4 |
| API et traitement métier | ASP.NET Core et SDK .NET 8, Entity Framework Core |
| Base de données | Microsoft SQL Server |
| Lecture PDF | Texte natif et service Python 3.12 avec PaddleOCR PP-OCRv6 et PP-StructureV3 |
| Déploiement | Scripts PowerShell sous Windows et Docker Compose pour le déploiement conteneurisé |

## Installation sous Windows

### 1. Récupérer les sources et vérifier les prérequis

Installez Git, le SDK .NET 8, Node.js avec npm, Python 3.12 x64 et SQL Server. Pour cette version de Next.js, utilisez Node.js 20.9 ou une version supérieure compatible. SQL Server Express convient à une installation locale. La base doit avoir un niveau de compatibilité 130 ou supérieur.

```powershell
git clone https://github.com/JENNAH-IMAD/Alias-convertpdf.git
Set-Location Alias-convertpdf
```

Vérifiez que la branche récupérée correspond à ce README. Si elle indique PostgreSQL, utilisez la copie SQL Server du projet ou sa branche lorsqu’elle est publiée.

### 2. Configurer SQL Server

Créez ou complétez le fichier local `backend/BankStatementConverter.API/appsettings.Development.json`.

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=Alias;Integrated Security=True"
  },
  "Database": {
    "AutoMigrate": true
  },
  "Seed": {
    "DemoData": false
  }
}
```

Adaptez le serveur et le nom de base à votre instance. Avec l’authentification Windows, le compte qui lance l’API doit avoir accès à SQL Server et les droits nécessaires pour appliquer les migrations. Utilisez une connexion sécurisée et gardez les identifiants dans la configuration locale.

Pour reprendre des données existantes, conservez la sauvegarde SQL Server, le dossier défini par `Storage:Root` et les clés de protection. L’outil `tools/SqlServerTransfer` permet de copier et vérifier une ancienne base PostgreSQL dont le schéma correspond au projet.

### 3. Installer les dépendances et lancer Alias

Depuis la racine du projet, installez les dépendances web.

```powershell
npm --prefix frontend ci
```

Dans un terminal dédié, installez et démarrez le moteur OCR. Les modèles sont téléchargés lors de leur première utilisation, puis mis en cache. Laissez le terminal ouvert.

```powershell
powershell -ExecutionPolicy Bypass -File services/ocr/start.ps1 -Install
```

Dans un deuxième terminal, lancez l’API et l’interface.

```powershell
.\Lancer-local.cmd
```

Ouvrez [Alias sur le port 3002](http://localhost:3002). À la première ouverture d’une base neuve, créez le compte administrateur. Pour les démarrages suivants, relancez le script OCR sans `-Install`, puis ouvrez `Lancer-local.cmd`.

### 4. Vérifier les services

```powershell
Invoke-RestMethod http://127.0.0.1:5081/health
Invoke-RestMethod http://127.0.0.1:8091/health
```

Les deux services doivent répondre avec un statut `ok`. Le premier démarrage de PaddleOCR peut prendre plus de temps pendant le chargement des modèles.

## Utiliser Alias

1. Dans **Banques et comptes**, préparez le client et son compte bancaire. Pour les écritures, renseignez le journal et le compte général.
2. Cliquez sur **Importer un PDF**. Choisissez le type de document, la banque, la devise et, si possible, le dossier client.
3. Sélectionnez le modèle d’exportation dans le parcours de conversion.
4. Comparez les mouvements au PDF original. Vérifiez dates, libellés, montants, sens débit/crédit et soldes. Corrigez et enregistrez.
5. Pour des écritures comptables, vérifiez les comptes de contrepartie et la TVA des commissions. Cliquez sur **Enregistrer et recalculer les écritures**, puis contrôlez l’équilibre.
6. Téléchargez le résultat ou archivez-le. Consultez ensuite **Espace clients** pour retrouver les documents et les fichiers associés.

## Documentation et maintenance

- Pour installer et lancer la version de ce dépôt, suivez les instructions ci-dessus.

`output` est ignoré par Git. Il peut contenir des éléments générés et des livrables locaux. Copiez les éléments à conserver avant de nettoyer ce dossier.

| Sous-dossier | Contenu à vérifier avant nettoyage |
| --- | --- |
| `output/installers`, `output/server-package`, `output/desktop-client` | Les paquets et installateurs locaux doivent être conservés ou régénérés si vous en avez encore besoin. |
| `output/runtime` | Certains lanceurs Windows utilisent ce runtime. Préservez sa configuration locale et reconstruisez-le avant de relancer ces modes si vous le supprimez. |
| Dossiers de migration, de transfert ou de snapshot | Vérifiez s’ils contiennent l’unique copie d’un export, d’une sauvegarde ou d’un rapport. |

Le script de nettoyage affiche d’abord les éléments ciblés. Utilisez `-Execute` après vérification.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Cleanup-GeneratedFiles.ps1
```

Pour vérifier le projet, utilisez `scripts/Check-Project.ps1`. Pour un déploiement, consultez le [guide serveur](installer/INSTALLATION.md) et le [guide desktop](installer/GUIDE-DESKTOP.md).
