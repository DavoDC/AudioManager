# Audio Tag Fixes Checklist - 2026-04-26

**Total Issues to Fix: 80** (3 Akira exceptions, 3 LibChecker mid-word "ft." false positives, 1 Coolio deleted, 3 legitimate false positives = 10 removed from original 90)

**Progress: ALL SECTIONS COMPLETE**
- Section 1 (27 tag fixes) ✓
- Section 2 (46 folder/metadata moves) ✓
- Section 3 (8 sources OST verification) ✓

**Ready to run AudioManager report to verify all 80 issues fixed and LibChecker clean.**

---

## SECTION 1: TAG & FILENAME CLEANUP (27 issues) - COMPLETE

Remove unwanted words from Title/Album/Filename tags in mp3tag.

- [x] Backstreet Boys - Everybody (Backstreet's Back) -- remove "edit"
- [x] Bone Thugs-N-Harmony;Akon - Never Forget Me (Album Version Explicit) -- remove "version" + "explicit" from title
- [x] Coolio;Snoop Dogg - Gangsta Walk (Urban Version) -- remove "version" from title/filename [DELETED]
- [x] Twista;CeeLo - Hope (Album Version) -- removed "version" from title/filename
  - **WATCH: Check for duplicate flag after fix - if triggered, duplicate checker needs to consider all featured artists**
- [x] Freeway;50 Cent - Take It To The Top (Album Version Explicit) -- remove "version" + "explicit" from title/filename
- [x] Akira The Don;Alan Watts - Beware of Virtue (20K Version) -- KEEP "version" (part of official title) - added to exceptions
- [x] Akira The Don;Alan Watts - The Highest Virtue (20K Version) -- KEEP "version" (part of official title) - added to exceptions
- [x] Simply Red - Holding Back the Years (2008 Remaster) -- remove "version" from album tag
- [x] Xzibit; Strong Arm Steady - Beware Of Us -- remove "explicit" from album tag
- [x] David Guetta;Kid Cudi - Memories -- remove "feat." from filename
- [x] Fort Minor;BOBO;Styles Of Beyond - Believe Me -- remove "feat." from filename
- [x] Fort Minor;Holly Brook;Jonah Matranga - Where'd You Go -- remove "feat." from filename
- [x] Fort Minor;John Legend - High Road -- remove "feat." from filename
- [LIBCHECKER BUG] Kodak Black - Identity Theft -- FALSE POSITIVE ("ft." is part of "Theft") - will be fixed by TIER 1 regex improvement
- [LIBCHECKER BUG] Lil Tecca - NEVER LEFT -- FALSE POSITIVE ("ft." is part of "LEFT") - will be fixed by TIER 1 regex improvement
- [x] Lupe Fiasco;Matthew Santos - Superstar -- remove "feat." from filename
- [x] Lupe Fiasco;Nikki Jean - Hip-Hop Saved My Life -- remove "feat." from filename
- [x] Plies;Akon - Hypnotized -- remove "feat." from filename
- [LIBCHECKER BUG] Russ - NO TEARS LEFT -- FALSE POSITIVE ("ft." is part of "LEFT") - will be fixed by TIER 1 regex improvement
- [x] Wiz Khalifa;Akon - Let It Go -- remove "feat." from filename
- [x] Chiddy Bang;Icona Pop - Mind Your Manners -- remove "feat." from filename
- [x] Maino;T-Pain - All the Above -- remove "feat." from filename
- [x] Mase;Total - What You Want -- remove "feat." from filename
- [x] P-Money;Akon - Keep on Calling -- remove "feat." from filename

---

## SECTION 2: ALBUM FOLDER ORGANIZATION (46 issues) - COMPLETE

Move files to correct album subfolder or Singles/.

- [x] Avril Lavigne - Fall To Pieces -- move to Under My Skin album folder
- [x] Avril Lavigne - My Happy Ending -- move to Under My Skin album folder
- [x] Bad Meets Evil;Eminem;Royce da 5'9 - Fast Lane -- move to Hell: The Sequel album folder
- [x] Bad Meets Evil;Eminem;Royce da 5'9;Bruno Mars - Lighters -- move to Hell: The Sequel album folder
- [x] Bob Dylan - Duquesne Whistle -- move to The Essential Bob Dylan album folder
- [x] Bob Dylan - Hurricane -- move to The Essential Bob Dylan album folder
- [x] Chris Brown;Too Short;E-40 - Undrunk -- move to Slime & B album folder
- [x] Chris Brown;Young Thug - Go Crazy -- move to Slime & B album folder
- [N/A] Coolio;Snoop Dogg - Gangsta Walk (Urban Version) -- move to Gangsta Walk album folder [DELETED]
- [N/A] Coolio;Snoop Dogg - Gangsta Walk -- move to Gangsta Walk album folder
- [x] Don Toliver - Get Throwed -- move to Life of a DON album folder
- [x] Don Toliver;Travis Scott - Flocky Flocky -- move to Life of a DON album folder
- [x] First Signal - Face Your Fears -- move to Face Your Fears album folder
- [x] First Signal - Shoot the Bullet -- move to Face Your Fears album folder
- [x] Future;Juice WRLD - Fine China -- move to WRLD ON DRUGS album folder
- [x] Future;Juice WRLD - Hard Work Pays Off -- move to WRLD ON DRUGS album folder
- [x] Hilltop Hoods - Cosby Sweater -- move to Walking Under Stars album folder
- [x] Hilltop Hoods;Maverick Sabre;Brother Ali - Live And Let Go -- move to Walking Under Stars album folder
- [x] Hopsin - Nocturnal Rainbows -- move to Raw album folder
- [x] Hopsin - Sag My Pants -- move to Raw album folder
- [x] Kanye West;Mr Hudson - Paranoid -- move to 808s & Heartbreak album folder
- [x] Kanye West;Young Jeezy - Amazing -- move to 808s & Heartbreak album folder
- [x] KSI - Low -- move to Thick Of It album folder
- [x] Lil Uzi Vert - Do What I Want -- move to The Perfect LUV Tape album folder
- [x] Lil Uzi Vert - Erase Your Social -- move to The Perfect LUV Tape album folder
- [x] Roddy Ricch - The Box -- move to Please Excuse Me for Being Antisocial album folder
- [x] Roddy Ricch;Mustard - High Fashion -- move to Please Excuse Me for Being Antisocial album folder
- [x] Taylor Swift - Stay Stay Stay -- move to Red album folder
- [x] Taylor Swift - We Are Never Ever Getting Back Together -- move to Red album folder
- [x] Too Short;Parliament Funkadelic - Gettin' It -- move to The Mack of the Century album folder
- [x] YoungBoy Never Broke Again - Dedicated -- move to AI YoungBoy album folder
- [x] YoungBoy Never Broke Again - No. 9 -- move to AI YoungBoy album folder

- [x] David Massengill - Fireball -- move from The Return/ to Singles/
- [x] David Massengill - Noah -- move from The Return/ to Singles/
- [x] David Massengill - You and Me -- move from The Return/ to Singles/
- [x] John Williamson - Bush Barber -- move from The Very Best of John Williamson/ to Singles/
- [x] John Williamson - Bushtown -- move from The Very Best of John Williamson/ to Singles/
- [x] John Williamson - Dad's Flowers -- move from The Very Best of John Williamson/ to Singles/
- [x] KSI;Lil Wayne - Lose -- move from Thick Of It/ to Singles/
- [x] Lady Gaga;Beyonce - Telephone -- move from The Fame/ to Singles/
- [x] Lil Wayne - Let It All Work Out -- move from Tha Carter V/ to Singles/
- [x] Michael Jackson;Akon - Hold My Hand -- move from Michael/ to Singles/
- [x] Moneybagg Yo - Scorpio -- move from A Gangsta's Pain/ to Singles/
- [x] Moneybagg Yo - Wockesha -- move from A Gangsta's Pain/ to Singles/
- [x] Phil Collins - I Wish It Would Rain Down (2016 Remaster) -- check album tag
- [x] Phil Collins - In the Air Tonight (2015 Remaster) -- check album tag

---

## SECTION 3: SOURCES FOLDER - VERIFY OST DESIGNATION (8 issues) - COMPLETE

**RULE:** Only add OST to album tag if the ALBUM itself IS the official soundtrack. Regular artist albums featured in shows/movies keep original album names.

- [x] a-ha - Take on Me (Super Mario Bros. Movie) -- KEEP album 'Hunting High and Low' (featured track, not OST)
- [x] Bonnie Tyler - Holding Out for a Hero (Super Mario Bros. Movie) -- KEEP album 'The Very Best of Bonnie Tyler' (featured track, not OST)
- [x] Electric Light Orchestra - Mr. Blue Sky (Super Mario Bros. Movie) -- KEEP album 'Out of the Blue' (featured track, not OST)
- [x] Dee Snider - We're Not Gonna Take It (Peacemaker) -- KEEP album 'We Are the Ones' (featured track, not OST)
- [x] Hanoi Rocks - Don't You Ever Leave Me (Peacemaker) -- KEEP album 'Two Steps From The Move' (featured track, not OST)
- [x] Pretty Maids - Little Drops Of Heaven (Peacemaker) -- KEEP album 'Pandemonium' (featured track, not OST)
- [x] Wig Wam - Do Ya Wanna Taste It (Peacemaker) -- KEEP album 'Non Stop Rock'n Roll' (featured track, not OST)
- [x] Cristobal Tapia de Veer - Aloha! (The White Lotus) -- VERIFIED album 'The White Lotus OST' (official soundtrack)

---

## DELETED TRACKS

- [x] Coolio;Snoop Dogg - Gangsta Walk (Urban Version) - removed from disk
