#!/usr/bin/perl
use strict;
use warnings;
use File::Find;

my($aacEncoder) = qw( C:/app/_console/neroAacEnc.exe );

chdir 'sound-wav' or die "chdir failed. $!";
for(<*.wav>){
	my $aac = "../sound/$_";
	$aac =~ s/\.wav$/.m4a/;
	
	# unlink $aac;
	next if -f $aac;
	
	my $cmd = qq('$aacEncoder' -br 128000 -he -ignorelength -if '$_' -of '$aac');
	print "$cmd\n";
	system $cmd;
}
