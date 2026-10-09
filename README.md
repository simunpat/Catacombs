# Catacombs

Et 2D top-down actionspil med roguelite-elementer, lavet i Unity som et Game Design-projekt af **Símun Pætur á Torkilsheyggi**.

Spilleren kæmper sig ned gennem fem etager i katakomberne. På hver etage vælger man rum gennem en gang og rydder fire kamre for at åbne bossdøren. Fjenderne bliver flere, og bosserne får nye angreb længere nede.

Efter de første fire bosser vælger man mellem helbredelse og to tilfældige opgraderinger. Opgraderinger beholdes resten af runnet. Den sidste boss giver sejr; dør spilleren, starter man forfra uden sine opgraderinger.

## Styring

- **WASD / piletaster:** Bevægelse.
- **Mus:** Sigt. Hold venstre museknap nede for at skyde.
- **Space:** Dash.
- **Esc:** Pause eller fortsæt.
- **T:** Vis et tip.
- **1, 2, 3 / mus:** Vælg en bossbelønning.
- **R:** Start et nyt run. På sejrsskærmen går R til hovedmenuen.

## Projektets opbygning

- `Assets/Scripts`: Spillets kode, blandt andet spiller, fjender, bosser og progression.
- `Assets/Scenes`: Hovedmenu, introduktion, gang og kamprum.
- `Assets/Catacombs`: Grafik, lyd, prefabs og opgraderinger.
- `Assets/Editor`: Værktøjer til opsætning og test i Unity Editor.
- `Packages` og `ProjectSettings`: Pakkeafhængigheder og projektindstillinger.

Til hurtig test kan **Catacombs > Testing Mode** i Unity bruges til at vælge etage og rum. Testtilstanden er kun tilgængelig i editoren, ikke i det eksporterede spil.

Flere detaljer findes i [projektets spil- og tekniknoter](Assets/Catacombs/READ-ME.txt).

## Lyd og credits

- **Lydeffekter:** [The Essential Retro Video Game Sound Effects Collection / 512 Sound Effects](https://opengameart.org/content/512-sound-effects-8-bit-style) af Juhani Junkala (SubspaceAudio), udgivet under **CC0**. Pakkens `INFO.txt` følger med i lydmappen.
- **Musik:** [High Quality 8-bit / Chiptune Musics](https://hydrogene.itch.io/high-quality-8-bit-musics) af HydroGene, udgivet under **CC0**. Spillet bruger *Strong Boss* i bosskampe, *MonsterVania #1* i normale kampe og *Infinite Darkness* uden for kamp.

CC0-oplysningerne gælder de nævnte lydpakker, ikke automatisk hele projektets kode eller Unity-indhold.
