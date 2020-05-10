#!/usr/bin/perl --
use strict;
use warnings;

# https://docs.microsoft.com/ja-jp/sysinternals/downloads/sigcheck
my($sigcheck) = qw( C:/app/_console/sigcheck64.exe );

# cygwin's zip command
my $zip = "/usr/bin/zip";


my($solutionDir,$outDir)= map{ my $b = $_; $b =~ s|\\|/|g; $b } (@ARGV,'','');
print "solutionDir=$solutionDir, outDir=$outDir\n";
$outDir or die "usage: $0 solutionDir outDir";

if( index( $outDir,"/bin/x64/Release/") >= 0 ){

	chdir($outDir);

	my $a = `$sigcheck -q StarGarner.dll`;
	$a =~ /Prod version:\s+(\S+)/ or die "can't find product version\n$a";
	my($version)=($1);

	my @lt = localtime;
	$lt[5]+=1900; $lt[4]+=1;
	my $zipfile = sprintf("${solutionDir}/StarGarner-%s-%d%02d%02d%02d%02d%02d.zip",$version,reverse @lt[0..5]);

	system qq($zip -r $zipfile * -x \@${solutionDir}/exclude.list  );

	chdir($solutionDir);
	system qq($zip $zipfile StarGarner.txt sound/*.m4a);
}


__END__

$ C:/app/_console/sigcheck64.exe -q StarGarner.dll

Sigcheck v2.73 - File version and signature viewer
Copyright (C) 2004-2019 Mark Russinovich
Sysinternals - www.sysinternals.com

C:\StarGarner\StarGarner\bin\x64\Release\netcoreapp3.1\StarGarner.dll:
        Verified:       Unsigned
        Link date:      9:23 1920/09/12
        Publisher:      n/a
        Company:        StarGarner
        Description:    StarGarner
        Product:        StarGarner
        Prod version:   1.0.1
        File version:   1.0.1.0
        MachineType:    64-bit
