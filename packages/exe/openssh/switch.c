/*
	SWITCH - Basic
	Author: Mark Bradshaw
	Email: mark@networksimplicity.com
	
	Switch attempts to make an intelligent choice as to what shell you need to
	have invoked in order to properly carry out your command.  It is compiled
	with gcc 2.95.3-5 under Cygwin (www.cygwin.com).

	When presented with no command (i.e. you just want a shell) it attempts to
	located and execute cmd.exe (windows standard shell).  When given a command
	to run (includes scp and sftp service) it switches to sh.exe as the correct
	shell.  It determines that a command has been issued by checking the first
	command line parameter.  It will be equal to "-c".
*/

int main (int argc, char *argv[ ]) {

	char command[255] = ""; 
	int a,i; 

	strncpy(command, getenv ("COMSPEC"), sizeof(command)-1);
	command[sizeof(command)-1] = '\0';

/*************************************************************************** 
Set command equal to the default location of the windows shell if ComSpec is
empty.  
***************************************************************************/
	if ( ! strcmp(command, "") ) {
		strncpy(command, getenv("SystemRoot"), sizeof(command) -1);
		command[sizeof(command)-1] = '\0';
		strncat(command, "/system32/cmd.exe", sizeof(command)-1-strlen(command));
		}

/***************************************************************************
There's a small problem that must be dealt with.  The cygwin "system" 
command will take any backslashes (\) and interpret them as escape 
characters.  Basically they'll disappear.  This will cause normal paths in
ComSpec, like "c:\winnt\system32\cmd.exe", to become "c:winntsystem32cmd.exe".
I switch all \'s to /'s.  Cygwin will take either.
***************************************************************************/
	for (a=0; a < strlen(command); a++) 
		if ( (char) command[a] == '\\' ) command[a] = '/';

		//add " /q" to the end of command to keep cmd.exe from repeating commands.
		strncat(command, " /q", sizeof(command) - 1 - strlen(command));

	// If switch gets any command line arguments then switch to /bin/sh
	if (argc>1 && !(strcmp(argv[1],"-c"))) {
		// Empty command.
		command[0]='\0';

		//Use /ssh/sh as the shell. 
		strncat(command, "/bin/sh.exe -c \"", sizeof(command)-1);
			
/***************************************************************************
Copy over the arguments allowing for up to 254 characters using strncat 
instead of strcat to protect against buffer overflow.  Start at 2 since the
"-c" isn't needed.
***************************************************************************/
		for(i=2; i < argc; i++){
			strncat(command, argv[i], sizeof(command)-1-strlen(command));
    		strncat(command, " ", sizeof(command)-1-strlen(command));
  			}
		// Finish up with a final quotation mark.
		strncat(command, "\"", sizeof(command)-1);
		}

	// User can specify -p on the command line to print out the command.  Used for debugging.
	if (argc>1 && !(strcmp(argv[1],"-p"))) printf("Command to execute: %s\n",command);

	// Use system to run the shell with whatever arguments it needs.
	system(command);
	return(0);
	}