#include <stdio.h>
#include <string.h>
#include <errno.h>
#include <signal.h>
#include <stdlib.h>

#include "hello.h"

#include "hello2.h"

#include "exists.c"

#include "another.h"

int main( int argc, char** argv )
{
#ifdef FOO
	printf(FOO);
#endif


#ifdef EXPAND
	printf("%s\n", EXPAND);
#endif

	return HELLO_TEST;
}