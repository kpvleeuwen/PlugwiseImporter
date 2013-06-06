PlugwiseImporter
================

Glue logic to update the SonnenErtrag (solar-yield) database with Plugwise readouts.
Can update PvOutput.org too.

Requirements: 
 * .Net 4.0 (client profile)
 * Microsoft Access installed (for the OLE driver)
 * Run as the same user that has Plugwise installed

Commandline summary:  
`list`                 Lists all appliances with ID in the plugwise database  
`appliances=<String>`  Comma-separated list of applianceIDs to use, default: all production  
`days=<Int32>`         Number of days to load, default: 14  
`to=<DateTime>`        Last day to load, defaults to today  
`verbose`              Give detailed error messages  
`sefacilityid=<Int32>` SonnenErtrag FacilityID, when missing SonnenErtrag uploading is disabled.  
`seuser=<String>`      SonnenErtrag user ID, default: ask  
`sepass=<String>`      SonnenErtrag password, default: ask  
`pvsystemid=<Int32>`   PVOutput.org System Id, when missing PVOutput uploading is disabled.  
`pvapikey=<String>`    PVOutput.org API Key, default: ask  
`csvfilename=<String>` CSV output file to use with PVOutput.org manual bulk uploading. Disabled when missing.  
`help`                 Displays this summary of supported arguments  

Clear skies!
