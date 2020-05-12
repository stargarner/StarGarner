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

	# バージョンを調べる
	my $a = `$sigcheck -q StarGarner.dll`;
	$a =~ /Prod version:\s+(\S+)/ or die "can't find product version\n$a";
	my($version)=($1);

	# zipファイル名
	my @lt = localtime;
	$lt[5]+=1900; $lt[4]+=1;
	my $zipfile = sprintf("${solutionDir}/StarGarner-%s-%d%02d%02d%02d%02d%02d.zip",$version,reverse @lt[0..5]);

	system qq($zip -r $zipfile * -x \@${solutionDir}/exclude.list  );

	chdir($solutionDir);
	
	system qq($zip $zipfile README.md sound/*.m4a);

	# apkファイル
	if( chdir("$solutionDir/StarGarnerCon") ){
		my $apk = `/usr/bin/ls -1at *.apk |/usr/bin/head -n 1`;
		$apk =~ s/[\x0d\x0a*]//g;
		if($apk){
			system qq($zip $zipfile $apk);
		}
	}
}

