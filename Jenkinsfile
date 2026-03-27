properties([
  parameters([

    choice(
      name: 'ENVIRONMENT',
      choices: ['DEV', 'PRODUCTION'],
      description: 'Select environment'
    ),

    choice(
      name: 'ACTION',
      choices: ['DEPLOY'],
      description: 'Deploy application'
    ),

    string(
      name: 'BRANCH',
      defaultValue: 'main',
      description: 'Git branch (DEV only)'
    ),

    string(
      name: 'ARTIFACT_BUILD',
      defaultValue: '',
      description: 'Build to deploy (only for PROD)'
    )

  ])
])

pipeline {

  agent { label 'windows-agent' }

  environment {
    GIT_REPO = 'https://github.com/1MRCV/studentmanagemnt.git'

    DEV_ARTIFACT_STORAGE  = 'C:\\jenkins-artifacts\\DEV'
    PROD_ARTIFACT_STORAGE = 'C:\\jenkins-artifacts\\PROD'

    DEV_IIS_PATH  = 'C:\\inetpub\\dev'
    PROD_IIS_PATH = 'C:\\inetpub\\prod'

    DEV_APP_POOL  = 'student-dev-pool'
    PROD_APP_POOL = 'student-prod-pool'

    DEV_SITE  = 'student-dev'
    PROD_SITE = 'student-prod'

    DEV_PORT  = '8081'
    PROD_PORT = '8082'

    PUBLISH_FOLDER = 'publish'
  }

  stages {

    stage('Set Build Name') {
      steps {
        script {
          def date = new Date().format('yyyy-MM-dd')
          currentBuild.displayName = "#${env.BUILD_NUMBER} | ${params.ENVIRONMENT} | ${date}"
        }
      }
    }

    // ================= DEV BUILD =================
    stage('Build & Publish (DEV only)') {
      when {
        expression { params.ENVIRONMENT == 'DEV' }
      }
      steps {
        deleteDir()

        checkout([
          $class: 'GitSCM',
          branches: [[name: "${params.BRANCH}"]],
          userRemoteConfigs: [[url: "${env.GIT_REPO}"]],
          gitTool: 'Git-Windows'
        ])

        powershell 'dotnet restore'
        powershell 'dotnet build --configuration Release'
        powershell 'dotnet publish StudentPortal.Web/StudentPortal.Web.csproj -c Release -o publish'
      }
    }

    // ================= CREATE ARTIFACT =================
    stage('Create Artifact (DEV only)') {
      when {
        expression { params.ENVIRONMENT == 'DEV' }
      }
      steps {
        powershell '''
          $build    = $env:BUILD_NUMBER
          $date     = Get-Date -Format "yyyy-MM-dd"
          $storage  = $env:DEV_ARTIFACT_STORAGE

          if (!(Test-Path $storage)) {
            New-Item -ItemType Directory -Path $storage | Out-Null
          }

          $folder = "$storage\\build_${build}_$date"
          New-Item -ItemType Directory -Path $folder -Force | Out-Null

          Compress-Archive `
            -Path "$env:PUBLISH_FOLDER\\*" `
            -DestinationPath "$folder\\artifact.zip" `
            -Force

          Write-Host "Artifact created: $folder"
        '''
      }
    }

    // ================= DEPLOY =================
    stage('Deploy to IIS') {
      steps {
        powershell '''
          $ErrorActionPreference = "Stop"

          # ✅ Correct variable usage
          $envName = $env:ENVIRONMENT

          Write-Host "ENVIRONMENT = $env:ENVIRONMENT"
          Write-Host "BUILD_NUMBER = $env:BUILD_NUMBER"
          Write-Host "ARTIFACT_BUILD = $env:ARTIFACT_BUILD"

          if ($envName -eq "DEV") {
              $buildNum = $env:BUILD_NUMBER
              $storage  = $env:DEV_ARTIFACT_STORAGE
          }
          else {
              $selected = $env:ARTIFACT_BUILD
              $buildNum = $selected.Split("|")[0].Replace("Build_","").Trim()
              $storage  = $env:PROD_ARTIFACT_STORAGE
          }

          Write-Host "Using BUILD: $buildNum"
          Write-Host "Using STORAGE: $storage"

          $folder = Get-ChildItem $storage -Directory |
                    Where-Object { $_.Name -like "build_${buildNum}_*" } |
                    Select-Object -First 1

          if ($null -eq $folder) {
              throw "Artifact not found for build $buildNum in $storage"
          }

          Write-Host "FOUND: $($folder.FullName)"

          $zipPath = Join-Path $folder.FullName "artifact.zip"

          Import-Module WebAdministration

          if ($envName -eq "DEV") {
              $deployPath  = $env:DEV_IIS_PATH
              $appPool     = $env:DEV_APP_POOL
              $websiteName = $env:DEV_SITE
              $port        = $env:DEV_PORT
          } else {
              $deployPath  = $env:PROD_IIS_PATH
              $appPool     = $env:PROD_APP_POOL
              $websiteName = $env:PROD_SITE
              $port        = $env:PROD_PORT
          }

          if (!(Test-Path "IIS:\\AppPools\\$appPool")) {
              New-WebAppPool -Name $appPool
          }

          if (!(Test-Path "IIS:\\Sites\\$websiteName")) {
              if (!(Test-Path $deployPath)) {
                  New-Item -ItemType Directory -Path $deployPath | Out-Null
              }

              New-Website -Name $websiteName -Port $port -PhysicalPath $deployPath -ApplicationPool $appPool
          }

          Stop-WebAppPool -Name $appPool -ErrorAction SilentlyContinue
          Start-Sleep -Seconds 2

          Remove-Item "$deployPath\\*" -Recurse -Force -ErrorAction SilentlyContinue

          Expand-Archive -Path $zipPath -DestinationPath $deployPath -Force

          Start-WebAppPool -Name $appPool
          Start-Website -Name $websiteName

          Write-Host "Deployment completed successfully"
        '''
      }
    }

    // ================= VERIFY =================
    stage('Verify') {
      steps {
        powershell '''
          Import-Module WebAdministration

          $appPool = if ($env:ENVIRONMENT -eq "DEV") { $env:DEV_APP_POOL } else { $env:PROD_APP_POOL }

          $state = (Get-WebAppPoolState -Name $appPool).Value

          Write-Host "App Pool State: $state"

          if ($state -ne "Started") {
              throw "Deployment failed"
          }

          Write-Host "Deployment verified successfully"
        '''
      }
    }

  }

  post {
    success {
      echo "SUCCESS: Build ${BUILD_NUMBER}"
    }
    failure {
      echo "FAILED: Check logs"
    }
  }

}
