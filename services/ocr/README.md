# Lecture PaddleOCR pour Alias

## Chaîne locale de traitement

Chaque lecture locale repart des octets du PDF original. Seuls les poids des modèles sont réutilisés.

1. Le backend lit le texte natif, ses positions et les zones en image. Une lecture
   native complète et cohérente n’est pas envoyée inutilement à l’OCR.
2. Les pages non résolues passent au service Python : rendu des pixels, détection de
   mise en page `PP-DocLayout-M`, OCR `PP-OCRv6` et reconstruction `PP-StructureV3`.
3. Le service renvoie les blocs OCR, leurs positions et scores, ainsi que des grilles
   de cellules et les limites des tableaux. Les cellules fusionnées sont développées
   sans dupliquer les valeurs. Les modules bancaires ne reçoivent pas le HTML brut.
4. Les cellules financières douteuses sont relues depuis leurs pixels. Une correction
   reconnue est propagée à la grille seulement si sa source est non ambiguë.
5. Le backend compare l’interprétation des cellules et celle des positions du même
   document. Il retient la lecture la plus complète, avec contrôle des dates, montants,
   couverture des pages et équation des soldes. Les divergences financières entre
   deux lectures cohérentes demandent une confirmation explicite.
6. Le PDF converti reconstruit les champs source en tableau. Les exports appliquent
   ensuite le modèle choisi aux mouvements reconnus et corrigés.

`OCR_PIPELINE=structure` est le défaut ; `OCR_PIPELINE=text` permet de diagnostiquer
le seul OCR. `OCR_LAYOUT_MODEL` et `OCR_MODEL_SIZE` choisissent les modèles locaux.
Le premier lancement télécharge leurs poids. `/health` expose `layout_ready` et la
version de chaîne `local-layout-v1`. Si la structure échoue, l’OCR positionnel reste
actif et une alerte indique le repli : aucun résultat ancien n’est substitué.

## Relevés scannés avec colonnes étroites

Le texte natif fiable est conservé ; les pages scannées ou
incomplètes passent à PaddleOCR local et à PP-StructureV3. Les pages restant
illisibles sont signalées pour contrôle ; aucune lecture externe supplémentaire
n’est déclenchée.

Les deux lectures et les différences de dates, libellés, références et montants
sont conservées. Une divergence n’écrase pas une première lecture saine : elle
apparaît dans « Lecture du document ». L’utilisateur corrige les valeurs puis
confirme explicitement sa vérification. Les dates manquantes/hors période, montants
manquants et désaccords non vérifiés bloquent les exports comptables, même avec
l’option de téléchargement avec erreurs. Les extraits suivent les mêmes règles.

La reconnaissance des tableaux rapproche les intitulés de colonnes sur plusieurs
niveaux, y compris les en-têtes bilingues et les intitulés arabes reconnus. Elle
vérifie la présence de lignes datées sous les colonnes. Le sens des montants suit
les titres débit/crédit, même lorsque leur ordre est inversé. Ces règles sont
communes aux relevés et aux extraits ; aucun modèle propre à une banque n’est requis.

Le moteur local relit également les cellules financières dont le séparateur décimal
a disparu dans le texte OCR : la correction doit être reconnue sur l’image de la
cellule avec une confiance suffisante, jamais déduite de l’équilibre des soldes.
Les données illisibles, les pages manquantes et les désaccords restent signalés.

Le moteur local relit les dates sous l’en-tête compact `OPER VALEUR` lorsque les
deux colonnes se touchent. Il retire les traits verticaux du recadrage et reconnaît
chaque paire de dates avant l’adaptation bancaire. Une décimale tronquée par une
bordure est relue directement dans son image, sans être complétée par calcul.

Le lecteur bancaire reconnaît ces intitulés CIH, les dates des soldes et le compte
dans la grille RIB. Il garde les deux dates séparées et n’utilise jamais le montant
du premier mouvement comme solde initial illisible. Les résultats OCR restent à
comparer au scan, notamment pour les libellés et les petits caractères.

## Moteur local PP-OCRv6

PaddleOCR local est utilisé pour les scans et les tableaux natifs non reconnus.

Le service effectue seulement l’étape de lecture : **PDF → images → blocs de texte**.
PaddleOCR PP-OCRv6 lit le texte français/latin avec ses positions et scores de
reconnaissance. L’API .NET adapte ensuite ces blocs en champs bancaires. Le moteur
d’export exécute les modèles CSV, Sage 100 et Sage X3 sur les données validées.

### Lancement Windows du moteur local

Python **3.12 64 bits** est requis pour le runtime PaddlePaddle retenu. Le Python
3.14 par défaut du poste ne doit pas être utilisé pour installer ce service.

```powershell
# Premier lancement sur un autre poste : crée .local/paddle-runtime et installe les dépendances.
powershell -ExecutionPolicy Bypass -File services/ocr/start.ps1 -Install
# Lancements suivants :
powershell -ExecutionPolicy Bypass -File services/ocr/start.ps1
```

Sur ce poste, le script réutilise le runtime compatible `.local/hybrid-runtime`
lorsqu’il existe. Ce dossier contient les bibliothèques Python installées ; il
ne constitue pas un retour de l’ancien traitement hybride.
Le script vérifie que l'interpréteur fonctionne en Python 3.12 et peut également
réutiliser `.local/python312/python.exe`. Cela évite de sélectionner un environnement
copié qui pointe vers le dossier utilisateur d'un autre poste. Les modèles sont
conservés dans `.local/paddle-cache`.

Le premier démarrage télécharge les modèles PaddleOCR. Attendre que
`http://127.0.0.1:8091/health` réponde `{"status":"ok",...}` avant un scan.
Le service traite les PDF **localement**. Les téléchargements initiaux des modèles
ne transmettent pas les documents bancaires. Les fichiers ne sont pas écrits dans
le service ; le PDF original et l’extraction sont conservés par l’application.

Lancer ensuite l’API et le frontend comme indiqué dans le README principal.
L’API utilise `Ocr:BaseUrl=http://127.0.0.1:8091` et `Ocr:TimeoutSeconds=0`
par défaut (aucun délai maximal de conversion). Ces valeurs peuvent être remplacées par `Ocr__BaseUrl` et
`Ocr__TimeoutSeconds`. Docker Compose lance aussi le service OCR.

## Contrat

`POST /extract` attend un formulaire multipart avec `file` (PDF, sans plafond de taille applicatif).
Le résultat contient `method=PADDLE_OCR` et `pages[]` : numéro, largeur, hauteur,
`boxes[]` avec texte, gauche/droite/haut/bas et confiance. Les coordonnées sont
en points PDF, avec l’origine en haut à gauche ; aucun montant bancaire n’est
calculé en Python. Les images sont limitées à 3 200 pixels par côté et les
documents n'ont plus de plafond de pages ni de taille PDF applicatif.
Chaque processus protège son instance OCR par un verrou.
Le lancement Windows et Docker utilise deux processus par défaut, pour permettre
des lectures parallèles. `start.ps1 -Workers 1` réduit la consommation mémoire ;
`OCR_CPU_THREADS` règle les threads par processus (deux par défaut). L'API possède
une file d'attente asynchrone et deux créneaux OCR configurables ; voir le
[guide de fonctionnement des conversions](../../README.md#traitement-des-relevés-et-extraits).
Un fichier vide, corrompu ou protégé
retourne une erreur explicite.

`OCR_MODEL_SIZE=small` est le réglage CPU par défaut ; `tiny` et `medium` restent
disponibles. La détection est plafonnée à 1 280 pixels sur le grand côté, réglable
avec `OCR_DETECTION_MAX_SIDE` ; les recadrages de reconnaissance conservent la
résolution du rendu PDF. Le point `/health` indique la taille du modèle chargé.
Une police PDF mal encodée peut déclencher l'OCR même sans image raster dans le PDF.

Le paramètre multipart facultatif `pages` sélectionne les numéros d'origine à lire.
Chaque import ou nouvelle lecture relance les moteurs nécessaires sur le PDF original,
même si ce fichier a déjà été traité. Aucun résultat de page PaddleOCR
n’est réutilisé entre traitements. Seuls les modèles OCR chargés restent en mémoire.
Une annulation arrête les pages suivantes
après la page en cours ; une relance recommence la lecture.
Le bouton **Nouvelle lecture du PDF** remplace les mouvements et corrections de la
version courante. Changer uniquement le modèle d’exportation conserve les données
revues, sans déclencher une nouvelle extraction.

## Adaptation et limites

Le score OCR indique la certitude du texte lu, pas la justesse comptable.
Les tableaux reconnus sont adaptés automatiquement. Si aucun mouvement ne peut
être reconnu, le texte lu reste enregistré et accessible dans **Texte lu & adaptation**.
Des positions de colonnes en pourcentage permettent d’adapter un autre tableau.
Ces positions communes supposent la même disposition sur toutes les pages ; des
mises en page différentes, des scans très flous, inclinés ou écrits à la main
peuvent nécessiter une adaptation supplémentaire. L’application bloque toujours
la validation d’un relevé sans mouvement ou comportant des montants ambigus.

## Vérification manuelle

Importer un PDF dans Alias avec le fournisseur OCR souhaité, puis comparer
chaque page et chaque mouvement au document original avant export.

Documentation officielle : [pipeline OCR PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR/blob/main/docs/version3.x/pipeline_usage/OCR.en.md).
