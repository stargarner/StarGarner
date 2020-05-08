#!/usr/bin/perl --
use strict;
use warnings;

my $zip = "/usr/bin/zip";

my($solutionDir,$outDir)= map{ my $b = $_; $b =~ s|\\|/|g; $b } (@ARGV,'','');
print "solutionDir=$solutionDir, outDir=$outDir\n";
$outDir or die "usage: $0 solutionDir outDir";

if( index( $outDir,"/bin/x64/Release/") >= 0 ){
	my @lt = localtime;
	$lt[5]+=1900; $lt[4]+=1;
	my $zipfile = sprintf("${solutionDir}/StarGarner-%d%02d%02d%02d%02d%02d.zip",reverse @lt[0..5]);

	chdir($outDir);
	system qq($zip -r $zipfile * -x \@${solutionDir}/exclude.list  );

	chdir($solutionDir);
	system qq($zip $zipfile StarGarner.txt sound/*.m4a);
}
